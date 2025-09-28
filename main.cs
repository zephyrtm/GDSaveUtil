using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using System.Linq;

class GDMerge
{
    const int XOR_KEY = 11;

    static void Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: GDSaveUtil <file1.dat> <file2.dat> <output.dat>");
            return;
        }

        string file1 = args[0];
        string file2 = args[1];
        string output = args[2];

        Console.WriteLine("Decoding input files...");

        // Decode both files to XML
        XDocument xml1 = DecodeDat(file1);
        XDocument xml2 = DecodeDat(file2);

        Console.WriteLine("Merging data...");

        // Merge XMLs
        XDocument merged = MergeGD(xml1, xml2);

        // Encode merged XML back to .dat
        EncodeDat(merged, output);

        Console.WriteLine("Merged save written to " + output);
    }

    // Decode .dat to XML
    static XDocument DecodeDat(string path)
    {
        byte[] encryptedData = File.ReadAllBytes(path);

        // Step 1: XOR decrypt
        byte[] xorData = new byte[encryptedData.Length];
        for (int i = 0; i < encryptedData.Length; i++)
            xorData[i] = (byte)(encryptedData[i] ^ XOR_KEY);

        // Step 2: Base64 decode (altchars - and _)
        string base64String = Encoding.UTF8.GetString(xorData)
            .Replace('-', '+').Replace('_', '/');
        // Add padding if needed
        int padding = 4 - (base64String.Length % 4);
        if (padding < 4) base64String += new string('=', padding);

        byte[] compressedData = Convert.FromBase64String(base64String);

        // Step 3: Skip first 10 bytes (GZip header) and decompress
        MemoryStream ms = new MemoryStream(compressedData, 10, compressedData.Length - 10);
        DeflateStream deflate = new DeflateStream(ms, CompressionMode.Decompress);
        MemoryStream decompressed = new MemoryStream();
        byte[] buffer = new byte[4096];
        int read;
        while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
        {
            decompressed.Write(buffer, 0, read);
        }
        deflate.Close();
        decompressed.Position = 0;

        StreamReader sr = new StreamReader(decompressed, Encoding.UTF8);
        string xmlText = sr.ReadToEnd();
        sr.Close();

        return XDocument.Parse(xmlText);
    }

    // Encode XML back to .dat
    static void EncodeDat(XDocument doc, string path)
    {
        string xmlText = doc.ToString(SaveOptions.DisableFormatting);
        byte[] xmlBytes = Encoding.UTF8.GetBytes(xmlText);

        // Step 1: Compress XML
        MemoryStream compressed = new MemoryStream();
        DeflateStream deflate = new DeflateStream(compressed, CompressionMode.Compress, true);
        deflate.Write(xmlBytes, 0, xmlBytes.Length);
        deflate.Close();

        byte[] compressedBytes = compressed.ToArray();

        // Step 2: Add fake GZip header (10 bytes)
        byte[] gzipData = new byte[compressedBytes.Length + 10];
        // Minimal gzip header
        gzipData[0] = 0x1f;
        gzipData[1] = 0x8b;
        gzipData[2] = 0x08;
        gzipData[3] = 0x00;
        gzipData[4] = gzipData[5] = gzipData[6] = gzipData[7] = 0x00;
        gzipData[8] = 0x00;
        gzipData[9] = 0x0b;
        Array.Copy(compressedBytes, 0, gzipData, 10, compressedBytes.Length);

        // Step 3: Base64 encode using altchars
        string base64 = Convert.ToBase64String(gzipData)
            .Replace('+', '-').Replace('/', '_');

        byte[] base64Bytes = Encoding.UTF8.GetBytes(base64);

        // Step 4: XOR encrypt
        byte[] encrypted = new byte[base64Bytes.Length];
        for (int i = 0; i < base64Bytes.Length; i++)
            encrypted[i] = (byte)(base64Bytes[i] ^ XOR_KEY);

        File.WriteAllBytes(path, encrypted);
    }

    // Merge XML files according to GD rules
    static XDocument MergeGD(XDocument a, XDocument b)
    {
        XElement rootA = a.Root;
        XElement rootB = b.Root;

        XDocument merged = new XDocument(new XElement(rootA));

        foreach (XElement elemB in rootB.Elements())
        {
            XElement elemA = merged.Root.Element(elemB.Name);
            if (elemA == null)
            {
                merged.Root.Add(new XElement(elemB));
            }
            else
            {
                int va, vb;

                // Numeric → max
                if (int.TryParse(elemA.Value, out va) &&
                    int.TryParse(elemB.Value, out vb))
                {
                    elemA.Value = Math.Max(va, vb).ToString();
                }
                // Boolean-like → OR
                else if ((elemA.Value == "1" || elemA.Value == "0") &&
                         (elemB.Value == "1" || elemB.Value == "0"))
                {
                    elemA.Value = (elemA.Value == "1" || elemB.Value == "1") ? "1" : "0";
                }
                // Comma-separated lists → union
                else if (elemA.Value.IndexOf(',') >= 0 || elemB.Value.IndexOf(',') >= 0)
                {
                    var set = elemA.Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Concat(elemB.Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        .Distinct();
                    elemA.Value = string.Join(",", set.ToArray());
                }
                // Default → keep A
            }
        }

        return merged;
    }
}
