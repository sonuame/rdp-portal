using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RDP_Portal {
    public partial class MainForm : Form {

        private Config _config;
        private bool _editMode = false;
        private Profile selectedProfile = null;
        private int selectedProfileIndex = -1;


        public MainForm() {
            InitializeComponent();
            _config = Config.GetConfig();
        }
        
        private void MainForm_Load(object sender, EventArgs e) {
            listBox.DataSource = _config.Profiles;

            cbResolutions.Items.AddRange(new[]
            {
                "1280 x 720",
                "1600 x 900",
                "Full Screen"
            });

            cbResolutions.SelectedIndex = 0;

            if (_config.Profiles.Count == 0)
            {
                AddNewProfile();
            }
        }

        public bool EditMode {
            get => _editMode;
            set {
                buttonEdit.Visible = !value;
                buttonSave.Visible = value;
                buttonCancel.Visible = value;
                buttonOptions.Enabled = !value;

                buttonConnect.Enabled = !value;

                textBoxName.Enabled = value;
                textBoxComputer.Enabled = value;
                textBoxUsername.Enabled = value;
                textBoxPassword.Enabled = value;
                textBoxDomain.Enabled = value;
            }
        }

        private void AddNewProfile() {
            var profile = new Profile();
            profile.Id = Guid.NewGuid().ToString();
            profile.JustAdded = true;
            profile.WindowSize = cbResolutions.Items[0].ToString();
            _config.Profiles.Add(profile);
            listBox.SelectedIndex = _config.Profiles.Count - 1;
        }

        private void buttonMoreOptions_Click(object sender, EventArgs e) {
            ProcessStartInfo startInfo = new ProcessStartInfo {
                CreateNoWindow = false,
                UseShellExecute = false,
                FileName = "mstsc.exe",
                Arguments = "/edit " + GetSelectedProfile().Filename,
            };

            try {
                var exeProcess = Process.Start(startInfo) ?? throw new InvalidOperationException();
                exeProcess.WaitForExit();
            } catch (Exception ex) {
                MessageBox.Show(ex.ToString());
            }
        }


        private void buttonConnect_Click(object sender, EventArgs e) {
            var profile = GetSelectedProfile();
            profile.WindowSize = cbResolutions.SelectedItem.ToString();

            if (String.IsNullOrWhiteSpace(profile.Computer) || String.IsNullOrWhiteSpace(profile.Computer)) {
                MessageBox.Show("Invalid connection");
                return;
            }

            profile.PrepareRdpFile();

            ProcessStartInfo startInfo = new ProcessStartInfo {
                CreateNoWindow = false,
                UseShellExecute = false,
                FileName = "mstsc.exe",
                Arguments = profile.Filename,
            };

            try {
                var exeProcess = Process.Start(startInfo) ?? throw new InvalidOperationException();
                exeProcess.WaitForExit();

                if (!_config.KeepOpening) {
                    this.Close();
                }

            } catch (Exception ex) {
                MessageBox.Show(ex.ToString());
            }
        }

        private void listBox_SelectedValueChanged(object sender, EventArgs e) {
            SelectProfile();
        }

        private Profile GetSelectedProfile() {
            return (Profile) listBox.SelectedItem;
        }

        private void SelectProfile(bool force = false) {
            var profile = (Profile) listBox.SelectedItem;

            // Avoid click empty area reset value
            if (profile == selectedProfile && !force) {
                btnDuplicate.Enabled = false;
                return;
            }

            selectedProfile = profile;
            selectedProfileIndex = _config.Profiles.IndexOf(profile);
            btnDuplicate.Enabled = true;
            EditMode = profile.JustAdded;

            textBoxName.Text = profile.Name ;
            textBoxComputer.Text = profile.Computer;
            textBoxUsername.Text = profile.Username ;
            textBoxPassword.Text = profile.Password;
            textBoxDomain.Text = profile.Domain;
            
        }

        private void buttonEdit_Click(object sender, EventArgs e) {
            EditMode = true;
        }

        private void buttonCancel_Click(object sender, EventArgs e) {
            EditMode = false;

            var profile = GetSelectedProfile();

            if (profile.JustAdded && _config.Profiles.Count > 1) {
                buttonDelete_Click(null, null);
            } else {
                SelectProfile(true);
            }
        }

        private void buttonNew_Click(object sender, EventArgs e) {
            AddNewProfile();
        }

        private void buttonDelete_Click(object sender, EventArgs e) {
            // show confirm dialog
            var confirmResult = MessageBox.Show(
                "Are you sure to delete this profile?",
                "Confirm",
                MessageBoxButtons.YesNo);

            // if confirm delete
            if (confirmResult == DialogResult.Yes) {
                var selectedItems = (Profile) listBox.SelectedItem;
                selectedItems.Delete();
                _config.Profiles.Remove(selectedItems);
                _config.Save();

                if (_config.Profiles.Count == 0) {
                    AddNewProfile();
                    SelectProfile(true);
                }
            }
        }

        private void buttonSave_Click(object sender, EventArgs e) {
            var profile = (Profile) listBox.SelectedItem;

            profile.JustAdded = false;

            profile.Name = textBoxName.Text;
            profile.Computer = textBoxComputer.Text;
            profile.Username = textBoxUsername.Text;
            profile.Password = textBoxPassword.Text;
            profile.Domain = textBoxDomain.Text;
            profile.Filename = Path.Combine(Config.rdpDir, profile.Name + ".rdp");
            profile.WindowSize = cbResolutions.SelectedItem.ToString();
            profile.PrepareRdpFile();

            _config.Save();
            EditMode = false;

            // Refresh the list
            listBox.DisplayMember = null;
            listBox.DisplayMember = "Name";
        }

        private void checkBoxKeepOpening_CheckedChanged(object sender, EventArgs e) {
            _config.KeepOpening = true;
            _config.Save();
        }

        private void buttonAbout_Click(object sender, EventArgs e) {
            About about = new About();
            about.ShowDialog(this);
        }

        private void listBox_MouseDoubleClick(object sender, MouseEventArgs e) {
            buttonConnect_Click(sender, e);
        }

        /**
         * From https://stackoverflow.com/questions/8333282/how-can-i-include-icons-in-my-listbox
         */
        private void listBox_DrawItem(object sender, DrawItemEventArgs e) {
            if (e.Index == -1)
                return;

            e.DrawBackground();
            Brush myBrush = Brushes.Black;


            var iconWidth = listBox.ItemHeight;
            var iconMargin = 4;
            var textMargin = (iconWidth - 18) / 2;
            var rect = new Rectangle(e.Bounds.X + iconMargin, e.Bounds.Y, iconWidth, iconWidth);
            //assuming the icon is already added to project resources

            e.Graphics.DrawIcon(RDP_Portal.Properties.Resources.icon, rect);

            var profile = (Profile)listBox.Items[e.Index];

            e.Graphics.DrawString(
                profile.Name,
                e.Font,
                myBrush,
                new Rectangle(e.Bounds.X + iconMargin * 2 + iconWidth, e.Bounds.Y + textMargin, e.Bounds.Width, e.Bounds.Height),
                StringFormat.GenericDefault
            );

            // If the ListBox has focus, draw a focus rectangle around the selected item.
            e.DrawFocusRectangle();
        }

        private void btnDuplicate_Click(object sender, EventArgs e)
        {
            var selectedItems = (Profile)listBox.SelectedItem;
            if(selectedItems != null)
            {
                AddNewProfile();
                textBoxName.Text = selectedItems.Name + "_copy";
                textBoxComputer.Text = selectedItems.Computer;
                textBoxDomain.Text = selectedItems.Domain;
                textBoxUsername.Text = selectedItems.Username;
                textBoxPassword.Text = selectedItems.Password;
            }
        }

        private void cbResolutions_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void listBox_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void btnUP_Click(object sender, EventArgs e)
        {
            var initial_profile = (Profile)listBox.SelectedItem;
            var initial_index = listBox.SelectedIndex;

            var new_item_index = initial_index > 0 ? initial_index - 1 : 0;
            if (new_item_index >= 0)
            {
                _config.Profiles.Insert(new_item_index, initial_profile);
                _config.Profiles.RemoveAt(initial_index + 1);
                _config.Save();

                listBox.DataSource = _config.Profiles;

                initial_index = _config.Profiles.ToList().FindIndex(m => m.Id == initial_profile.Id);

                if (initial_index > -1)
                    listBox.SelectedIndex = initial_index;
            }
        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            var initial_profile = (Profile)listBox.SelectedItem;
            var initial_index = listBox.SelectedIndex;

            var new_item_index = initial_index < (_config.Profiles.Count - 1) ? initial_index + 2 : (_config.Profiles.Count - 1);
            if (initial_index < (_config.Profiles.Count - 1))
            {
                _config.Profiles.Insert(new_item_index, initial_profile);
                _config.Profiles.RemoveAt(initial_index);
                _config.Save();

                listBox.DataSource = _config.Profiles;

                initial_index = _config.Profiles.ToList().FindIndex(m => m.Id == initial_profile.Id);

                if (initial_index < (_config.Profiles.Count))
                    listBox.SelectedIndex = initial_index;
            }
        }
    }
}
