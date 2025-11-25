using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace GeometryDashSaveMerger
{
    public class SaveFileProcessor
    {
        private const string XOR_KEY = "11";
        private const string SAVE_HEADER = "<?xml version=\"1.0\"?>";

        public async Task<string> DecryptSaveFileAsync(string filePath)
        {
            try
            {
                byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
                
                // Apply XOR decryption
                for (int i = 0; i < fileBytes.Length; i++)
                {
                    fileBytes[i] ^= (byte)XOR_KEY[i % XOR_KEY.Length];
                }

                string decryptedContent = Encoding.UTF8.GetString(fileBytes);
                
                // Remove null characters and trim
                decryptedContent = decryptedContent.Replace("\0", "").Trim();
                
                // Ensure it starts with proper XML header
                if (!decryptedContent.StartsWith(SAVE_HEADER))
                {
                    // Try to fix common issues
                    if (decryptedContent.Contains(SAVE_HEADER))
                    {
                        int headerIndex = decryptedContent.IndexOf(SAVE_HEADER);
                        decryptedContent = decryptedContent.Substring(headerIndex);
                    }
                    else
                    {
                        decryptedContent = SAVE_HEADER + "<plist>" + decryptedContent + "</plist>";
                    }
                }

                return decryptedContent;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to decrypt file: {ex.Message}");
            }
        }

        public async Task EncryptAndSaveAsync(string content, string filePath)
        {
            try
            {
                byte[] contentBytes = Encoding.UTF8.GetBytes(content);
                
                // Apply XOR encryption
                for (int i = 0; i < contentBytes.Length; i++)
                {
                    contentBytes[i] ^= (byte)XOR_KEY[i % XOR_KEY.Length];
                }

                await File.WriteAllBytesAsync(filePath, contentBytes);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to encrypt and save file: {ex.Message}");
            }
        }

        public string MergeSaveFiles(string content1, string content2)
        {
            try
            {
                // Simple merge strategy - take the longer content
                // In a real implementation, you'd parse the XML and merge specific values
                return content1.Length > content2.Length ? content1 : content2;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to merge save files: {ex.Message}");
            }
        }

        public string GetPreviewText(string content, int maxLength = 200)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            if (content.Length > maxLength)
            {
                return content.Substring(0, maxLength) + "...";
            }
            return content;
        }
    }
}