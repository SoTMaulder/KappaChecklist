using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using OpenCvSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;

namespace KappaChecklist
{
    public partial class Form1 : Form
    {
        private const string ConfigFile = "collector_progress.json";
        private const string ImagesFolder = "images";
        private Dictionary<string, bool> progress;
        private List<string> allItems;
        private Dictionary<string, CheckBox> checkBoxMap;
        private Dictionary<string, string> itemImagePaths;
        private Panel checklistPanel;
        private bool isClosing = false;
        private Dictionary<string, bool> previousDetectionState;

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public Form1()
        {
            InitializeComponent();
            AllocConsole();  // Open the console window for debugging
            Console.Title = "Kappa Checklist Debugger";  // Set the console window title
            PositionConsole(); // Move the console to the correct position
            SetConsoleTopMost(); // Keep console on top
            checklistPanel = new Panel();
            LoadProgress();
            InitializeItemImagePaths();
            ConfigureForm();
            CreateChecklist();
            StartManualScan(); // Start the manual scan trigger
            this.FormClosing += OnFormClosing;
            previousDetectionState = new Dictionary<string, bool>();
            Console.WriteLine("Application started. Press Enter to manually scan for images.");
        }



        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint TOPMOST_FLAGS = SWP_NOMOVE | SWP_NOSIZE;

        private void SetConsoleTopMost()
        {
            IntPtr consoleWindow = Process.GetCurrentProcess().MainWindowHandle;
            SetWindowPos(consoleWindow, HWND_TOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS);
        }



        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            isClosing = true;
        }

        private void ConfigureForm()
        {
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;  // Keep GUI on top

            int screenWidth = Screen.PrimaryScreen.WorkingArea.Width;
            int screenHeight = Screen.PrimaryScreen.WorkingArea.Height;

            int guiWidth = 400;
            int guiHeight = 1000;

            guiWidth = Math.Min(guiWidth, screenWidth);
            guiHeight = Math.Min(guiHeight, screenHeight);

            int guiX = screenWidth - guiWidth;
            int guiY = screenHeight - guiHeight;

            this.Location = new System.Drawing.Point(guiX, guiY);
            this.Size = new System.Drawing.Size(guiWidth, guiHeight);
            this.BackColor = Color.Black;

            checklistPanel.Location = new System.Drawing.Point(0, 0);
            checklistPanel.Size = new System.Drawing.Size(this.Width, this.Height);
            checklistPanel.AutoScroll = true;
            checklistPanel.BackColor = Color.FromArgb(50, 50, 50);
            this.Controls.Add(checklistPanel);
        }




        private void SaveProgress()
        {
            try
            {
                string json = JsonSerializer.Serialize(progress, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFile, json);
                Console.WriteLine("Progress saved successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving progress: {ex.Message}");
            }
        }


        private void LoadProgress()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    string json = File.ReadAllText(ConfigFile);
                    progress = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                    allItems = progress.Keys.ToList();
                    Console.WriteLine("Progress loaded successfully.");
                }
                else
                {
                    Console.WriteLine("Config file not found. Creating new progress data.");
                    allItems = new List<string>
            {
                "Old firesteel", "Antique axe", "Battered antique book", "FireKlean gun lube", "Golden rooster figurine",
                "Silver Badge", "Deadlyslob's beard oil", "Golden 1GPhone smartphone", "Jar of DevilDog mayo",
                "Can of sprats", "Fake mustache", "Kotton beanie", "Raven figurine", "Pestily plague mask",
                "Shroud half-mask", "Can of Dr Lupos coffee beans", "42 Signature Blend English Tea", "Veritas guitar pick",
                "Armband (Evasion)", "Can of RatCola soda", "Loot Lord plushie", "WZ Wallet", "LVNDMARK's rat poison",
                "Smoke balaclava", "Missam forklift key", "Video cassette with the Cyborg Killer movie",
                "BakeEzy cook book", "JohnB Liquid DNB glasses", "Glorious E lightweight armored mask",
                "Baddie's red beard", "DRD body armor", "Gingy keychain", "Golden egg", "Press pass (issued for NoiceGuy)",
                "Axel parrot figurine", "BEAR Buddy plush toy", "Inseq gas pipe wrench", "Viibiin sneaker",
                "Tamatthi kunai knife replica"
            };
                    progress = allItems.ToDictionary(item => item, item => false);
                    SaveProgress(); // Save the newly created default progress
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading progress: {ex.Message}");
                allItems = new List<string>();
                progress = new Dictionary<string, bool>();
            }
        }


        private async void StartManualScan()
        {
            Console.WriteLine("Press Enter to scan for images manually.");

            while (!isClosing)
            {
                // Wait for Enter key press to initiate the scan
                await Task.Run(() => Console.ReadLine());

                if (isClosing) break;

                Console.WriteLine("Starting manual scan...");
                await Task.Run(() => DetectImagesOnScreen());
                Console.WriteLine("Scan complete. Press Enter to scan again.");
            }
        }





        private void PrintWithColor(string message, ConsoleColor color)
        {
            // Clear the current line to avoid color conflicts
            Console.ResetColor();
            Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r");  // Clear the line

            // Set the color, print the message, and immediately reset the color
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();  // Explicitly reset to ensure no lingering color
        }



        private void LogDetectionStatus(string item, bool isDetected)
        {
            if (!previousDetectionState.ContainsKey(item) || previousDetectionState[item] != isDetected)
            {
                previousDetectionState[item] = isDetected;
                string status = isDetected ? "Detected" : "Not Detected";
                ConsoleColor color = isDetected ? ConsoleColor.Green : ConsoleColor.Red;

                // Use the dedicated method for consistent and correct color output
                PrintWithColor($"{DateTime.Now:HH:mm:ss} - {status}: {item}", color);
            }
        }





        [DllImport("user32.dll")]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        private void PositionConsole()
        {
            IntPtr consoleWindow = Process.GetCurrentProcess().MainWindowHandle;
            int consoleWidth = 800;
            int consoleHeight = 300;

            int screenWidth = Screen.PrimaryScreen.WorkingArea.Width;
            int screenHeight = Screen.PrimaryScreen.WorkingArea.Height;

            // Adjust console size if it exceeds screen dimensions
            consoleWidth = Math.Min(consoleWidth, screenWidth / 2);
            consoleHeight = Math.Min(consoleHeight, screenHeight / 3);

            // Position console at the bottom left of the screen
            int consoleX = 0;
            int consoleY = screenHeight - consoleHeight;

            MoveWindow(consoleWindow, consoleX, consoleY, consoleWidth, consoleHeight, true); // Bottom left
        }




        private bool DetectImageInEFT(string imagePath)
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("EscapeFromTarkov");
                if (processes.Length == 0)
                {
                    Console.WriteLine("EscapeFromTarkov process not found.");
                    return false;
                }

                IntPtr hwnd = processes[0].MainWindowHandle;
                if (GetWindowRect(hwnd, out RECT rect))
                {
                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;

                    using (Bitmap bmp = new Bitmap(width, height))
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new System.Drawing.Size(width, height));
                        Mat screenMat = OpenCvSharp.Extensions.BitmapConverter.ToMat(bmp);
                        Cv2.CvtColor(screenMat, screenMat, ColorConversionCodes.BGR2GRAY);
                        Cv2.Normalize(screenMat, screenMat, 0, 255, NormTypes.MinMax);

                        using (Mat template = Cv2.ImRead(imagePath, ImreadModes.Grayscale))
                        {
                            if (screenMat.Empty() || template.Empty())
                            {
                                Console.WriteLine($"Error: Screen or template image is empty for {imagePath}");
                                return false;
                            }

                            Cv2.Normalize(template, template, 0, 255, NormTypes.MinMax);
                            Mat result = new Mat();
                            Cv2.MatchTemplate(screenMat, template, result, TemplateMatchModes.CCoeffNormed);
                            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out _);

                            bool isDetected = maxVal >= 0.9; // Increased sensitivity
                            Console.WriteLine(isDetected ? $"Highly Accurate Match: {imagePath}" : $"Not Detected: {imagePath}");
                            return isDetected;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting image {imagePath}: {ex.Message}");
                return false;
            }

            return false;
        }

        private void DetectImagesOnScreen()
        {
            foreach (var item in allItems)
            {
                if (isClosing) return;

                if (itemImagePaths.TryGetValue(item, out string imagePath))
                {
                    bool isDetected = DetectImageInEFT(imagePath);
                    LogDetectionStatus(item, isDetected);

                    // Update the UI asynchronously to prevent freezing
                    Invoke((MethodInvoker)(() =>
                    {
                        progress[item] = isDetected;
                        checkBoxMap[item].Checked = isDetected;
                        UpdateCheckboxColor(checkBoxMap[item], isDetected);
                    }));
                }
            }
            SaveProgress();
        }


        private void UpdateCheckboxColor(CheckBox checkBox, bool isChecked)
        {
            checkBox.BackColor = isChecked ? Color.FromArgb(150, 0, 255, 0) : Color.FromArgb(150, 255, 0, 0);
        }

        private void InitializeItemImagePaths()
        {
            itemImagePaths = new Dictionary<string, string>();
            foreach (var item in allItems)
            {
                string sanitizedItem = item.ToLower()
                    .Replace(" ", "_")
                    .Replace("'", "")
                    .Replace("(", "")
                    .Replace(")", "")
                    .Replace("#", "")
                    .Replace(",", "")
                    .Replace("-", "_");

                string imagePath = Path.Combine(ImagesFolder, sanitizedItem + ".png");
                if (File.Exists(imagePath))
                {
                    itemImagePaths[item] = imagePath;
                }
            }
        }

        private void CreateChecklist()
        {
            checkBoxMap = new Dictionary<string, CheckBox>();
            int y = 50;

            foreach (string item in allItems)
            {
                Panel itemPanel = new Panel
                {
                    Size = new System.Drawing.Size(300, 100),
                    Location = new System.Drawing.Point(50, y),
                    BackColor = Color.Transparent
                };

                if (itemImagePaths.ContainsKey(item))
                {
                    PictureBox pictureBox = new PictureBox
                    {
                        Image = Image.FromFile(itemImagePaths[item]),
                        SizeMode = PictureBoxSizeMode.StretchImage,
                        Size = new System.Drawing.Size(60, 60),
                        Location = new System.Drawing.Point(0, 20)
                    };
                    itemPanel.Controls.Add(pictureBox);
                }

                CheckBox checkBox = new CheckBox
                {
                    Text = item,
                    AutoSize = true,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Popup,
                    Location = new System.Drawing.Point(80, 25)
                };

                // Set the checkbox state based on loaded progress
                if (progress.TryGetValue(item, out bool isChecked))
                {
                    checkBox.Checked = isChecked;
                    UpdateCheckboxColor(checkBox, isChecked); // Update color based on state
                }
                else
                {
                    checkBox.Checked = false;
                    UpdateCheckboxColor(checkBox, false); // Default color
                }

                checkBox.CheckedChanged += (s, e) =>
                {
                    bool isChecked = checkBox.Checked;
                    UpdateCheckboxColor(checkBox, isChecked);
                    progress[item] = isChecked;
                    SaveProgress();
                };

                checkBoxMap[item] = checkBox;
                itemPanel.Controls.Add(checkBox);
                checklistPanel.Controls.Add(itemPanel);
                y += 120;
            }
        }

    }
}
