using Microsoft.Win32;
using Microsoft.Scripting.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit;
using IronPython.Hosting;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private float quantizeFactor = 12.5f;

        Point startPosition;
        Point endPosition;
        double startPositionX;
        double endPositionX;
        float previewLength;
        int previewRow;
        int previewColumn;
        int previewStartMargin;
        private bool mouseDown = false;
        public MainWindow()
        {
            InitializeComponent();
        }

        public string FindTextBetween(string text, string left, string right)
        {
            // TODO: Validate input arguments

            int beginIndex = text.IndexOf(left); // find occurence of left delimiter
            if (beginIndex == -1)
                return string.Empty; // or throw exception?

            beginIndex += left.Length;

            int endIndex = text.IndexOf(right, beginIndex); // find occurence of right delimiter
            if (endIndex == -1)
                return string.Empty; // or throw exception?

            return text.Substring(beginIndex, endIndex - beginIndex).Trim();
        }

        private void run_script(string projectFile, string voicebankName)
        {
            string command = string.Format(@"/C py main.py ""{0}"" ""{1}""", projectFile, voicebankName);

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Verb = "runas",
                Arguments = command,
                UseShellExecute = false,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/RealWorldMath"
            };

            Process.Start(startInfo);
        }

        private double RoundToNearest(double number, float nearest)
        {
            return Math.Round(number / nearest) * nearest;
        }

        private Tuple<int, int, int> GetNearest(Point position)
        {
            int row = Convert.ToInt32(MathF.Floor(Convert.ToSingle(position.Y / 23)));
            int column = Convert.ToInt32(MathF.Floor(Convert.ToSingle(position.X / 100)));
            int offset = Convert.ToInt32(Math.Round(position.X % 100 / quantizeFactor) * quantizeFactor);

            if (offset == 100)
            {
                offset = 0;
                column++;
            }

            return Tuple.Create(row, column, offset);
        }

        private Grid AddNote(int row, int column, SolidColorBrush outline, SolidColorBrush fill, Grid grid, double length, double startMargin, string lyric="a")
        {
            double newLength;
            if (length < 0)
            {
                newLength = 0;
            } else
            {
                newLength = length;
            }
            Grid newGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch,
                Width = newLength * 100
            };
            newGrid.RowDefinitions.Add(new RowDefinition());
            newGrid.ColumnDefinitions.Add(new ColumnDefinition());
            Rectangle newRectangle = new Rectangle
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 0),
                Stroke = outline,
                Fill = fill
            };
            TextBox label = new TextBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 3, 2, 3),
                Text = lyric
            };
            newGrid.PreviewMouseRightButtonUp += new MouseButtonEventHandler(newGrid_MouseRightButtonUp);
            newGrid.Children.Add(newRectangle);
            newGrid.Children.Add(label);
            grid.Children.Add(newGrid);
            Grid.SetColumn(newGrid, column);
            Grid.SetRow(newGrid, row);
            Grid.SetColumnSpan(newGrid, Convert.ToInt32(Math.Floor(newLength) + 2));
            newGrid.Margin = new Thickness(startMargin, 0, 0, 0);

            return newGrid;
        }

        private void AddColumns(int amount, Grid grid)
        {
            for (int i = 0; i < amount; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition());
            }
            grid.Width += 100 * amount;
        }

        private void On_ColumnButtonClick(object sender, RoutedEventArgs e)
        {
            AddColumns(columnUpDown.Value.GetValueOrDefault(), pianoRollGrid);
        }

        private void On_ExitClick(object sender, RoutedEventArgs e)
        {
            mainWindow.Close();
        }

        private void On_ImportClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Filter = "All Project Files | *.txt; *.ust|Text Files (*.txt)|*.txt|UTAU Project Files (*.ust)|*.ust",
                FileName = "notes.txt",
                RestoreDirectory = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\RealWorldMath"
            };

            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                foreach (UIElement element in pianoRollGrid.Children)
                {
                    pianoRollGrid.Children.Remove(element);
                }
                if (openFileDialog.FileName.Split('.')[^1] == "txt")
                {
                    string[] projectFile = File.ReadAllLines(openFileDialog.FileName);
                    foreach (var (note, index) in projectFile.WithIndex())
                    {
                        if (note.StartsWith("[#"))
                        {
                            int row = 108 - int.Parse(projectFile[index + 4].Split('=')[1]);
                            int col = (int)Math.Floor(double.Parse(projectFile[index + 1].Split('=')[1]) / 480);
                            double startMargin = double.Parse(projectFile[index + 1].Split('=')[1]) % 480 / 480 * 100;
                            string lyric = projectFile[index + 3].Split('=')[1];
                            double length = double.Parse(projectFile[index + 2].Split('=')[1]) / 480;
                            Debug.WriteLine(string.Format("Row: {0}, Column: {1}, Length: {2}, Start Margin: {3}, Lyric: {4}", row, col, length, startMargin, lyric));
                            while (col * 480 + length + startMargin >= (pianoRollGrid.ColumnDefinitions.Count - 1) * 480)
                            {
                                AddColumns(1, pianoRollGrid);
                            }
                            AddNote(row, col, Brushes.Blue, Brushes.LightBlue, pianoRollGrid, length, startMargin, lyric);
                        }
                        else if (note.StartsWith("Tempo="))
                        {
                            tempoUpDown.Value = int.Parse(note.Split('=')[1]);
                        }
                    }
                }
                else if (openFileDialog.FileName.Split('.')[^1] == "ust")
                {
                    string[] projectFile = File.ReadAllLines(openFileDialog.FileName);

                    double previousLengths = 0;
                    foreach (var (note, index) in projectFile.WithIndex())
                    {
                        if (note.StartsWith("Tempo="))
                        {
                            tempoUpDown.Value = (int) double.Parse(note.Split('=')[1]);
                        }
                        else if (note.StartsWith("[#"))
                        {
                            if (int.TryParse(FindTextBetween(note, "[#", "]"), out int _tempint))
                            {
                                double startPos = 0;
                                int row = 0; //108 - int.Parse(projectFile[index + 4].Split('=')[1]);
                                int col = 0; //(int)Math.Floor(double.Parse(projectFile[index + 1].Split('=')[1]) / 480);
                                double startMargin; //double.Parse(projectFile[index + 1].Split('=')[1]) % 480 / 480 * 100;
                                string lyric = "a";
                                double length = 480;

                                string[] projectFileAfterCurrentNote = projectFile[(index+1)..^0];
                                foreach (var (betweenNote, i) in projectFileAfterCurrentNote.WithIndex())
                                {
                                    if (betweenNote.StartsWith("[#"))
                                    {
                                        if (lyric != "R")
                                        {
                                            Debug.WriteLine(previousLengths);
                                            Debug.WriteLine("Adding note");
                                            startMargin = previousLengths % 480 / 480 * 100;
                                            col = (int)Math.Floor(previousLengths / 480);
                                            startPos = previousLengths;
                                            //do stuff (Addnote, Addlength to total length)
                                            AddNote(row, col, Brushes.Blue, Brushes.LightBlue, pianoRollGrid, length, startMargin, lyric);
                                        }
                                        previousLengths += length * 480;
                                        while (previousLengths >= pianoRollGrid.ColumnDefinitions.Count * 480)
                                        {
                                            AddColumns(1, pianoRollGrid);
                                        }
                                        break;
                                    } else if (betweenNote.StartsWith("Length="))
                                    {
                                        Debug.WriteLine("Set length");
                                        length = double.Parse(betweenNote.Split('=')[1]) / 480;
                                    }
                                    if (betweenNote.StartsWith("Lyric="))
                                    {
                                        Debug.WriteLine("Set lyric");
                                        lyric = betweenNote.Split('=')[1];
                                    }
                                    else if (betweenNote.StartsWith("NoteNum="))
                                    {
                                        Debug.WriteLine("Set notenum");
                                        row = 108 - int.Parse(betweenNote.Split('=')[1]);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void On_ExportClick(object sender, RoutedEventArgs e)
        {
            var orderByResult = from Grid note in pianoRollGrid.Children
                                orderby Grid.GetColumn(note), note.Margin.Left
                                select note;

            string fileContents = "";

            fileContents += "Tempo=" + tempoUpDown.Value + "\n";

            foreach (var (note, index) in orderByResult.WithIndex()){
                Grid newGrid = (Grid)note;
                fileContents += string.Format("[#{0}]\n", (index + 1).ToString().PadLeft(4, '0'));
                int startPos = 0;
                startPos += Grid.GetColumn(newGrid) * 480;
                startPos += (int)Math.Round(newGrid.Margin.Left / 100 * 480);
                fileContents += "StartPos=" + startPos + "\n";
                fileContents += "Length=" + (int)Math.Round(newGrid.Width / 100 * 480) + "\n";
                fileContents += "Lyric=" + ((TextBox)newGrid.Children[1]).Text + "\n";
                int key = 88 - Grid.GetRow(newGrid) + 20;
                fileContents += "Note=" + key + "\n";
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog()
            {
                Filter = "txt files (*.txt)|*.txt",
                FileName = "notes.txt",
                RestoreDirectory = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\RealWorldMath"
            };

            bool? result = saveFileDialog.ShowDialog();
            if (result == true)
            {
                File.WriteAllText(saveFileDialog.FileName, fileContents);
            }

            string tempString = saveFileDialog.FileName.Replace(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\RealWorldMath\\", "").Remove(saveFileDialog.FileName.Replace(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\RealWorldMath\\", "").Length - 4);
            noteFileTextBox.Text = tempString.Replace("\\", "/");
        }

        private void On_QuantizeSelectionChange(object sender, SelectionChangedEventArgs e)
        {
            if (quantizeComboBox.SelectedIndex == 0)
            {
                quantizeFactor = 12.5f;
            } else if (quantizeComboBox.SelectedIndex == 1)
            {
                quantizeFactor = 25;
            } else if (quantizeComboBox.SelectedIndex == 2)
            {
                quantizeFactor = 50;
            }
            else if (quantizeComboBox.SelectedIndex == 3)
            {
                quantizeFactor = 100;
            }
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            startPosition = e.GetPosition(pianoRollGrid);
            HitTestResult result = VisualTreeHelper.HitTest(pianoRollGrid, startPosition);
            if (result != null)
            {
                if (result.VisualHit.GetType() != pianoRollGrid.GetType())
                {
                    return;
                }
            }
            startPositionX = RoundToNearest(startPosition.X, quantizeFactor);
            previewRow = GetNearest(startPosition).Item1;
            previewColumn = GetNearest(startPosition).Item2;
            previewStartMargin = GetNearest(startPosition).Item3;
            mouseDown = true;
        }

        private void pianoRollGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (mouseDown == true)
            {
                endPosition = e.GetPosition(pianoRollGrid);
                endPositionX = RoundToNearest(endPosition.X, quantizeFactor);
                previewLength = (float)(Math.Round(endPositionX - startPositionX) / 100f);
                Debug.WriteLine("Start Position: {0}, End Position: {1}, Length: {2}", startPosition, endPosition, previewLength);
                if (previewLength != 0)
                {
                    AddNote(previewRow, previewColumn, Brushes.Blue, Brushes.LightBlue, pianoRollGrid, previewLength, previewStartMargin);
                }
                mouseDown = false;
            }
        }

        private void newGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            UIElement uiElement = (UIElement)sender;

            pianoRollGrid.Children.Remove(uiElement);
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            ScrollViewer scrollViewer = (ScrollViewer)sender;

            if (keyScrollViewer.VerticalOffset != scrollViewer.VerticalOffset)
            {
                keyScrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset);
            }
        }

        private void OnNoteLeftMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.GetPosition(pianoRollGrid);
        }

            private void keyScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            ScrollViewer scrollViewer = (ScrollViewer)sender;

            if (pianoRollScrollViewer.VerticalOffset != scrollViewer.VerticalOffset)
            {
                scrollViewer.ScrollToVerticalOffset(pianoRollScrollViewer.VerticalOffset);
            }
        }

        private void mainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            pianoRollScrollViewer.Width = mainWindow.Width - 80;
        }



        private void generateAudioButton_Click(object sender, RoutedEventArgs e)
        {
            string language;
            string voice;

            if (languageComboBox.Text == "English")
            {
                language = "ENG";
            } else if (languageComboBox.Text == "French")
            {
                language = "FRE";
            } else if (languageComboBox.Text == "Chinese")
            {
                language = "CHN";
            }
            else if (languageComboBox.Text == "Japanese")
            {
                language = "JPN";
            }
            else
            {
                language = "CAN";
            }

            if (voiceComboBox.Text == "Male")
            {
                voice = "M";
            }
            else
            {
                voice = "F";
            }

            run_script(noteFileTextBox.Text, string.Format("{0}-{1}1", language, voice));
        }
    }
}
