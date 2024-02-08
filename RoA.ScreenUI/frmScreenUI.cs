using Newtonsoft.Json;
using RoA.Points;
using RoA.Screen;
using RoA.Screen.State.External;
using RoA.Screen.State.Internal;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace RoA.ScreenUI
{
    public partial class frmScreenUI : Form
    {
        ConfigurationFile configFile;
        private string _rockerConfigPath;
        private bool _watching = false;

        public frmScreenUI()
        {
            InitializeComponent();
        }

        private Process gameProcess = null;

        public Process FindProcess()
        {
            Process[] processes = Process.GetProcessesByName("RivalsofAether");

            foreach (var process in processes)
            {
                if (process.MainWindowHandle == IntPtr.Zero)
                {
                    continue;
                }

                return process;
            }

            return null;
        }

        private void bgwSync_DoWork(object sender, DoWorkEventArgs e)
        {
            ScreenSyncer syncer = new ScreenSyncer();

            gameProcess = FindProcess();

            if (gameProcess == null)
            {
                MessageBox.Show("No RivalsofAether.exe executable is currently running.");
                SetNotWatching();
            }

            while (true)
            {
                if (!_watching)
                {
                    return;
                }

                try
                {
                    Bitmap screen;

                    if (rbtnWindowed.Checked)
                    {
                        ScreenTools.BringProcessWindowToFront(gameProcess);

                        screen = ScreenTools.CaptureFromWindow(gameProcess);
                        screen = ScreenTools.ResizeImage(screen, 1920, 1080);
                        //screen.Save("C:\\Users\\robob\\Documents\\ShareX\\testWindowedFight2.bmp", ImageFormat.Bmp);
                    }
                    else
                    {
                        //Bitmap screen = ScreenTools.CaptureFromScreen(new Rectangle(0, 0, 2560, 1440), new Size(1920, 1080));
                        screen = ScreenTools.CaptureFromScreen(new Rectangle(0, 0, 1920, 1080), null);
                        //screen.Save("C:\\Users\\robob\\Documents\\ShareX\\testDesktopFight.bmp", ImageFormat.Bmp);
                    }
                    var stateResultsTuple = syncer.Sync(screen);

                    bool changesOccurred = stateResultsTuple.Item1;
                    ScreenState currentState = stateResultsTuple.Item2;

                    if (changesOccurred)
                    {
                        bgwSync.ReportProgress(0, currentState);
                        WriteStateToFile(new ExternalScreenState(currentState));
                    }

                    screen.Dispose();
                    Thread.Sleep(250);
                }
                catch (Exception) { }
            }
        }

        private void bgwSync_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage == 0)
            {
                UpdateFormLabels((ScreenState)e.UserState);
            }
            else if (e.ProgressPercentage == 1)
            {
                this.Close();
            }
        }

        private void UpdateFormLabels(ScreenState s)
        {
            if (!String.IsNullOrEmpty(s.P1Character.SlotState)) grpPlayer1.Text = $"Player 1 ({s.P1Character.SlotState})";
            if (!String.IsNullOrEmpty(s.P2Character.SlotState)) grpPlayer2.Text = $"Player 2 ({s.P2Character.SlotState})";
            if (!String.IsNullOrEmpty(s.P3Character.SlotState)) grpPlayer3.Text = $"Player 3 ({s.P3Character.SlotState})";
            if (!String.IsNullOrEmpty(s.P4Character.SlotState)) grpPlayer4.Text = $"Player 4 ({s.P4Character.SlotState})";

            lblP1Character.Text = s.P1Character.Character;
            lblP2Character.Text = s.P2Character.Character;
            lblP3Character.Text = s.P3Character.Character;
            lblP4Character.Text = s.P4Character.Character;

            lblP1Stocks.Text = s.P1Character.Stocks;
            lblP2Stocks.Text = s.P2Character.Stocks;
            lblP3Stocks.Text = s.P3Character.Stocks;
            lblP4Stocks.Text = s.P4Character.Stocks;

            lblP1Games.Text = s.TourneySet.P1GameCount;
            lblP2Games.Text = s.TourneySet.P2GameCount;
            lblP3Games.Text = s.TourneySet.P3GameCount;
            lblP4Games.Text = s.TourneySet.P4GameCount;

            lblP1Damage.Text = s.P1Character.Damage;
            lblP2Damage.Text = s.P2Character.Damage;
            lblP3Damage.Text = s.P3Character.Damage;
            lblP4Damage.Text = s.P4Character.Damage;

            lblBestOf.Text = s.TourneySet.TourneyModeBestOf;
            lblStocks.Text = s.TourneySet.Stocks;
            lblTime.Text = s.TourneySet.Time;
            lblInMatch.Text = s.TourneySet.InMatch;
        }

        private void frmScreenUI_Load(object sender, EventArgs e)
        {
            string rockerPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _rockerConfigPath = String.Format("{0}\\RockerConfig.json", rockerPath);

            if (!File.Exists(_rockerConfigPath))
            {
                try
                {
                    ConfigurationFile configFile = new ConfigurationFile();
                    configFile.Save(_rockerConfigPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not create RocketConfig.json configuration file: " + ex.Message.ToString(), "Error");
                }
            }
            else
            {
                try
                {
                    string configText = File.ReadAllText(_rockerConfigPath);
                    configFile = JsonConvert.DeserializeObject<ConfigurationFile>(configText);

                    txtSaveLocation.Text = configFile.StateSavePath;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error reading in RocketConfig.json file: " + ex.Message.ToString(), "Error");
                }
            }
        }

        private void btnSaveDirectory_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog brws = new SaveFileDialog())
            {
                brws.FileName = "RoAState.json";
                brws.Filter = "json file (*.json)|*.json";
                if (brws.ShowDialog() == DialogResult.Cancel) return;

                string sPrevLocation = configFile.StateSavePath == null ? "" : configFile.StateSavePath;
                configFile.StateSavePath = brws.FileName;

                try
                {
                    configFile.Save(_rockerConfigPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error saving RockerConfig.json file: " + ex.Message.ToString(), "Error");
                    configFile.StateSavePath = sPrevLocation;
                }
                txtSaveLocation.Text = configFile.StateSavePath;
            }
        }

        private void WriteStateToFile(ExternalScreenState state)
        {
            try
            {
                if (!String.IsNullOrEmpty(configFile.StateSavePath))
                {
                    string json = JsonConvert.SerializeObject(state, Formatting.Indented);
                    File.WriteAllText(configFile.StateSavePath, json);
                }
            }
            catch (Exception) { } // Silently error, don't want to muck up game.
        }

        private void btnSync_Click(object sender, EventArgs e)
        {
            if (!_watching)
            {
                SetWatching();
            }
            else
            {
                SetNotWatching();
            }
        }

        private void SetWatching()
        {
            _watching = true;
            btnSync.Text = "Stop Watching";
            bgwSync.RunWorkerAsync();
        }

        private void SetNotWatching()
        {
            btnSync.Text = "Watch Screen";
            _watching = false;
        }
    }
}
