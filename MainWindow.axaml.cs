using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GeometryDashSaveMerger
{
    public partial class MainWindow : Window
    {
        private readonly SaveFileProcessor _processor;

        public MainWindow()
        {
            InitializeComponent();
            _processor = new SaveFileProcessor();
        }

        private async void OnSelectFile1Click(object sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select First Save File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Geometry Dash Save Files")
                    {
                        Patterns = new[] { "*.dat" }
                    },
                    new FilePickerFileType("All Files")
                    {
                        Patterns = new[] { "*.*" }
                    }
                }
            });

            if (files.Count > 0)
            {
                TxtFile1.Text = files[0].Path.LocalPath;
                await UpdatePreviewAsync();
            }
        }

        private async void OnSelectFile2Click(object sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Second Save File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Geometry Dash Save Files")
                    {
                        Patterns = new[] { "*.dat" }
                    },
                    new FilePickerFileType("All Files")
                    {
                        Patterns = new[] { "*.*" }
                    }
                }
            });

            if (files.Count > 0)
            {
                TxtFile2.Text = files[0].Path.LocalPath;
                await UpdatePreviewAsync();
            }
        }

        private async Task UpdatePreviewAsync()
        {
            if (!string.IsNullOrEmpty(TxtFile1.Text) && !string.IsNullOrEmpty(TxtFile2.Text))
            {
                try
                {
                    string decrypted1 = await _processor.DecryptSaveFileAsync(TxtFile1.Text);
                    string decrypted2 = await _processor.DecryptSaveFileAsync(TxtFile2.Text);
                    
                    TxtPreview1.Text = _processor.GetPreviewText(decrypted1);
                    TxtPreview2.Text = _processor.GetPreviewText(decrypted2);
                    
                    LblStatus.Text = "Files loaded successfully. Ready to merge.";
                    LblStatus.Foreground = new SolidColorBrush(Colors.LightGreen);
                }
                catch (Exception ex)
                {
                    LblStatus.Text = $"Error: {ex.Message}";
                    LblStatus.Foreground = new SolidColorBrush(Colors.LightCoral);
                }
            }
        }

        private async void OnMergeClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtFile1.Text) || string.IsNullOrEmpty(TxtFile2.Text))
            {
                await ShowErrorDialog("Please select both save files first.");
                return;
            }

            try
            {
                // Decrypt both files
                string decrypted1 = await _processor.DecryptSaveFileAsync(TxtFile1.Text);
                string decrypted2 = await _processor.DecryptSaveFileAsync(TxtFile2.Text);
                
                // Merge the files
                string mergedContent = _processor.MergeSaveFiles(decrypted1, decrypted2);
                
                // Save the merged file
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save Merged File",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("Geometry Dash Save Files")
                        {
                            Patterns = new[] { "*.dat" }
                        }
                    },
                    SuggestedFileName = "CCGameManager_merged.dat"
                });

                if (file != null)
                {
                    await _processor.EncryptAndSaveAsync(mergedContent, file.Path.LocalPath);
                    
                    LblStatus.Text = "Files merged successfully!";
                    LblStatus.Foreground = new SolidColorBrush(Colors.LightGreen);
                    
                    await ShowInfoDialog($"Save files merged successfully!\nSaved to: {file.Path.LocalPath}");
                }
            }
            catch (Exception ex)
            {
                LblStatus.Text = $"Merge failed: {ex.Message}";
                LblStatus.Foreground = new SolidColorBrush(Colors.LightCoral);
                await ShowErrorDialog($"Failed to merge files: {ex.Message}");
            }
        }

        private async void OnDecryptOnlyClick(object sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Save File to Decrypt",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Geometry Dash Save Files")
                    {
                        Patterns = new[] { "*.dat" }
                    }
                }
            });

            if (files.Count > 0)
            {
                try
                {
                    string decryptedContent = await _processor.DecryptSaveFileAsync(files[0].Path.LocalPath);
                    
                    var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Save Decrypted File",
                        FileTypeChoices = new[]
                        {
                            new FilePickerFileType("XML Files")
                            {
                                Patterns = new[] { "*.xml" }
                            },
                            new FilePickerFileType("Text Files")
                            {
                                Patterns = new[] { "*.txt" }
                            }
                        },
                        SuggestedFileName = Path.GetFileNameWithoutExtension(files[0].Name) + "_decrypted.xml"
                    });

                    if (file != null)
                    {
                        await File.WriteAllTextAsync(file.Path.LocalPath, decryptedContent);
                        await ShowInfoDialog($"File decrypted successfully!\nSaved to: {file.Path.LocalPath}");
                    }
                }
                catch (Exception ex)
                {
                    await ShowErrorDialog($"Failed to decrypt file: {ex.Message}");
                }
            }
        }

        private async Task ShowErrorDialog(string message)
        {
            var dialog = new Window
            {
                Title = "Error",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.Manual
            };

            var stackPanel = new StackPanel
            {
                Spacing = 10,
                Margin = new Thickness(20)
            };

            var textBlock = new TextBlock 
            { 
                Text = message, 
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };

            var button = new Button 
            { 
                Content = "OK", 
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, 
                Width = 80
            };

            button.Click += (s, e) => dialog.Close();

            stackPanel.Children.Add(textBlock);
            stackPanel.Children.Add(button);
            dialog.Content = stackPanel;
            
            await dialog.ShowDialog(this);
        }

        private async Task ShowInfoDialog(string message)
        {
            var dialog = new Window
            {
                Title = "Success",
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.Manual
            };

            var stackPanel = new StackPanel
            {
                Spacing = 10,
                Margin = new Thickness(20)
            };

            var textBlock = new TextBlock 
            { 
                Text = message, 
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };

            var button = new Button 
            { 
                Content = "OK", 
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, 
                Width = 80
            };

            button.Click += (s, e) => dialog.Close();

            stackPanel.Children.Add(textBlock);
            stackPanel.Children.Add(button);
            dialog.Content = stackPanel;
            
            await dialog.ShowDialog(this);
        }
    }
}