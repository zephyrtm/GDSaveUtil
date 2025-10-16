using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;

class GDMerge
{
    const int XOR_KEY = 11;

    [STAThread]
    static void Main()
    {
        try
        {
            string startFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GeometryDash");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Pick first file
            OpenFileDialog ofd1 = new OpenFileDialog();
            ofd1.InitialDirectory = startFolder;
            ofd1.Filter = "Geometry Dash save (*.dat)|*.dat|All files|*.*";
            ofd1.Title = "Select first Geometry Dash save (e.g. CCGameManager.dat)";
            if (ofd1.ShowDialog() != DialogResult.OK) return;
            string file1 = ofd1.FileName;

            // Pick second file
            OpenFileDialog ofd2 = new OpenFileDialog();
            ofd2.InitialDirectory = startFolder;
            ofd2.Filter = "Geometry Dash save (*.dat)|*.dat|All files|*.*";
            ofd2.Title = "Select second Geometry Dash save (e.g. CCGameManager.dat)";
            if (ofd2.ShowDialog() != DialogResult.OK) return;
            string file2 = ofd2.FileName;

            // Output save location
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.InitialDirectory = startFolder;
            sfd.Filter = "Geometry Dash save (*.dat)|*.dat|All files|*.*";
            sfd.FileName = "merged_CCGameManager.dat";
            sfd.Title = "Save merged file as...";
            if (sfd.ShowDialog() != DialogResult.OK) return;
            string output = sfd.FileName;

            Console.WriteLine("Decoding input files...");
            XDocument xml1 = DecodeDat(file1);
            XDocument xml2 = DecodeDat(file2);

            Console.WriteLine("Merging data...");
            XDocument merged = MergeGD(xml1, xml2);

            Console.WriteLine("Encoding merged file...");
            EncodeDat(merged, output);

            MessageBox.Show("Merged save written to:\n" + output, "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("An error occurred:\n" + ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ---------- Decoding / Encoding (matches the Python behavior) ----------

    static XDocument DecodeDat(string path)
    {
        byte[] encryptedData = File.ReadAllBytes(path);

        // XOR with key
        byte[] xorData = new byte[encryptedData.Length];
        for (int i = 0; i < encryptedData.Length; i++) xorData[i] = (byte)(encryptedData[i] ^ XOR_KEY);

        // Interpret as UTF8 string, replace altchars to standard base64
        string base64 = Encoding.UTF8.GetString(xorData)
            .Replace('-', '+').Replace('_', '/');

        // Add padding if missing
        int pad = base64.Length % 4;
        if (pad != 0) base64 = base64 + new string('=', 4 - pad);

        byte[] gzipLike = Convert.FromBase64String(base64);

        // gzipLike structure: [10-byte gzip header][deflate bytes][4-byte CRC32][4-byte ISIZE]
        if (gzipLike.Length < 18) throw new InvalidDataException("Decoded data too small to be a valid save.");

        int deflateStart = 10;
        int deflateLength = gzipLike.Length - 10 - 8; // minus 8 bytes for CRC32 + ISIZE
        if (deflateLength <= 0) throw new InvalidDataException("Invalid compressed data length.");

        MemoryStream msDeflate = new MemoryStream(gzipLike, deflateStart, deflateLength);
        DeflateStream ds = new DeflateStream(msDeflate, CompressionMode.Decompress);
        MemoryStream msOut = new MemoryStream();
        byte[] buffer = new byte[4096];
        int r;
        while ((r = ds.Read(buffer, 0, buffer.Length)) > 0) msOut.Write(buffer, 0, r);
        ds.Close();
        msOut.Position = 0;

        StreamReader sr = new StreamReader(msOut, Encoding.UTF8);
        string xmlText = sr.ReadToEnd();
        sr.Close();

        return XDocument.Parse(xmlText);
    }

    static void EncodeDat(XDocument doc, string path)
    {
        string xmlText = doc.ToString(SaveOptions.DisableFormatting);
        byte[] xmlBytes = Encoding.UTF8.GetBytes(xmlText);

        // Deflate (raw deflate bytes)
        MemoryStream msDeflateOut = new MemoryStream();
        DeflateStream dsOut = new DeflateStream(msDeflateOut, CompressionMode.Compress, true);
        dsOut.Write(xmlBytes, 0, xmlBytes.Length);
        dsOut.Close();
        byte[] deflateBytes = msDeflateOut.ToArray();

        // Build gzip-like data: header (10 bytes) + deflate bytes + crc32 + isize
        byte[] gzipHeader = new byte[10];
        gzipHeader[0] = 0x1f;
        gzipHeader[1] = 0x8b;
        gzipHeader[2] = 0x08;
        gzipHeader[3] = 0x00;
        gzipHeader[4] = gzipHeader[5] = gzipHeader[6] = gzipHeader[7] = 0x00;
        gzipHeader[8] = 0x00;
        gzipHeader[9] = 0x0b;

        uint crc = Crc32(xmlBytes);
        uint isize = (uint)xmlBytes.Length;

        MemoryStream msAll = new MemoryStream();
        msAll.Write(gzipHeader, 0, gzipHeader.Length);
        msAll.Write(deflateBytes, 0, deflateBytes.Length);
        msAll.Write(BitConverter.GetBytes(crc), 0, 4);   // little-endian
        msAll.Write(BitConverter.GetBytes(isize), 0, 4); // little-endian

        byte[] gzipLike = msAll.ToArray();

        // Base64 encode with altchars
        string base64 = Convert.ToBase64String(gzipLike).Replace('+', '-').Replace('/', '_');

        // XOR encrypt
        byte[] base64Bytes = Encoding.UTF8.GetBytes(base64);
        byte[] encrypted = new byte[base64Bytes.Length];
        for (int i = 0; i < base64Bytes.Length; i++) encrypted[i] = (byte)(base64Bytes[i] ^ XOR_KEY);

        File.WriteAllBytes(path, encrypted);
    }

    // ---------- CRC32 (IEEE 802.3) ----------
    static uint Crc32(byte[] data)
    {
        uint[] table = CrcTable;
        uint crc = 0xFFFFFFFFu;
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            crc = (crc >> 8) ^ table[(crc ^ b) & 0xFF];
        }
        return crc ^ 0xFFFFFFFFu;
    }

    static uint[] _crcTable = null;
    static uint[] CrcTable
    {
        get
        {
            if (_crcTable != null) return _crcTable;
            _crcTable = new uint[256];
            const uint poly = 0xEDB88320u;
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((c & 1) != 0) c = poly ^ (c >> 1);
                    else c = c >> 1;
                }
                _crcTable[i] = c;
            }
            return _crcTable;
        }
    }

    // ---------- Merge logic for GD XML dict style ----------

    static XDocument MergeGD(XDocument aDoc, XDocument bDoc)
    {
        // Try to find the topmost dict elements in both documents.
        XElement aRootDict = FindTopDict(aDoc.Root);
        XElement bRootDict = FindTopDict(bDoc.Root);

        if (aRootDict == null || bRootDict == null)
        {
            // fallback: if structure not recognized, just return aDoc
            return aDoc;
        }

        // Clone A as base result
        XElement mergedRootDict = new XElement(aRootDict);

        MergeDictElements(mergedRootDict, bRootDict);

        // Place mergedRootDict back into a new doc using the same outer root name as aDoc
        XDocument result = new XDocument(new XElement(aDoc.Root.Name, mergedRootDict));
        return result;
    }

    // Find the primary <dict> element (search children)
    static XElement FindTopDict(XElement root)
    {
        if (root == null) return null;
        if (root.Name.LocalName.ToLower().Contains("dict")) return root;
        // sometimes the XML root wraps the dict
        XElement dict = root.Descendants().FirstOrDefault(x => x.Name.LocalName.ToLower().Contains("dict"));
        return dict;
    }

    // Merge dictB into dictA (both are <dict> elements where keys are <k> nodes then a value node)
    static void MergeDictElements(XElement dictA, XElement dictB)
    {
        // Build key->value node map for A
        Dictionary<string, XElement> mapA = BuildDictMap(dictA);

        // Iterate keys in B and merge
        List<XElement> bChildren = dictB.Elements().ToList();
        for (int i = 0; i < bChildren.Count; i++)
        {
            XElement child = bChildren[i];
            if (child.Name.LocalName != "k") continue;
            string key = child.Value;
            // value is next element node
            XElement valueB = null;
            if (i + 1 < bChildren.Count) valueB = bChildren[i + 1];

            XElement valueA;
            if (!mapA.TryGetValue(key, out valueA))
            {
                // Key not present in A => copy both <k> and value element at end of dictA
                dictA.Add(new XElement(child));
                if (valueB != null) dictA.Add(new XElement(valueB));
            }
            else
            {
                // merge valueA and valueB
                if (valueB == null) continue;
                MergeValueNodes(valueA, valueB);
            }
        }
    }

    static Dictionary<string, XElement> BuildDictMap(XElement dict)
    {
        Dictionary<string, XElement> map = new Dictionary<string, XElement>();
        List<XElement> children = dict.Elements().ToList();
        for (int i = 0; i < children.Count; i++)
        {
            XElement c = children[i];
            if (c.Name.LocalName == "k")
            {
                string key = c.Value;
                XElement value = null;
                if (i + 1 < children.Count) value = children[i + 1];
                if (value != null && !map.ContainsKey(key)) map.Add(key, value);
            }
        }
        return map;
    }

    // Merge two value nodes: valueA will be mutated to the merged result with valueB
    static void MergeValueNodes(XElement valueA, XElement valueB)
    {
        // If both are dicts -> recurse
        if (IsDictElement(valueA) && IsDictElement(valueB))
        {
            MergeDictElements(valueA, valueB);
            return;
        }

        // If both are simple text-bearing nodes, compare values
        string aText = GetNodeText(valueA);
        string bText = GetNodeText(valueB);

        // Try numeric
        int va, vb;
        if (int.TryParse(aText, out va) && int.TryParse(bText, out vb))
        {
            valueA.Value = Math.Max(va, vb).ToString();
            return;
        }

        // Try boolean-like '1'/'0'
        if ((aText == "1" || aText == "0") && (bText == "1" || bText == "0"))
        {
            valueA.Value = (aText == "1" || bText == "1") ? "1" : "0";
            return;
        }

        // Try comma-separated lists -> union
        if ((aText != null && aText.Contains(",")) || (bText != null && bText.Contains(",")))
        {
            string[] aParts = SplitList(aText);
            string[] bParts = SplitList(bText);
            var union = aParts.Concat(bParts).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToArray();
            valueA.Value = string.Join(",", union);
            return;
        }

        // Otherwise: if valueA is empty and valueB has content -> take B
        if (string.IsNullOrEmpty(aText) && !string.IsNullOrEmpty(bText))
        {
            valueA.Value = bText;
            return;
        }

        // Default: keep A (game merges usually prefer highest/narrower changes; keeping A avoids overwriting)
    }

    static bool IsDictElement(XElement e)
    {
        if (e == null) return false;
        return e.Elements().Any(x => x.Name.LocalName == "k");
    }

    static string GetNodeText(XElement node)
    {
        if (node == null) return "";
        // If node has nested simple text (like <s>text</s>), return its text
        if (!node.HasElements) return node.Value ?? "";
        // If node has a child that is text-bearing, prefer that child's value
        XElement firstLeaf = node.Elements().FirstOrDefault(x => !x.HasElements);
        return (firstLeaf != null) ? firstLeaf.Value ?? "" : node.Value ?? "";
    }

    static string[] SplitList(string s)
    {
        if (string.IsNullOrEmpty(s)) return new string[0];
        return s.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim()).ToArray();
    }
}
