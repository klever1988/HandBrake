/*  frmQueue.cs $
 	
 	   This file is part of the HandBrake source code.
 	   Homepage: <http://handbrake.fr>.
 	   It may be used under the terms of the GNU General Public License. */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;

namespace Handbrake
{
    public partial class frmQueue : Form
    {
        private delegate void ProgressUpdateHandler();
        private delegate void setEncoding();
        Functions.CLI cliObj = new Functions.CLI();
        Boolean cancel = false;
        Process hbProc = null;
        Functions.Queue queue;

        public frmQueue()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Initializes the Queue list with the Arraylist from the Queue class
        /// </summary>
        /// <param name="qw"></param>
        public void setQueue(Functions.Queue qw)
        {
            queue = qw;
            redrawQueue();
        }

        /// <summary>
        /// Returns if there is currently an item being encoded by the queue
        /// </summary>
        /// <returns>Boolean true if encoding</returns>
        public Boolean isEncoding()
        {
            if (hbProc == null)
                return false;
            else
                return true;
        }

        // Redraw's the queue with the latest data from the Queue class
        private void redrawQueue()
        {
            list_queue.Items.Clear();
            foreach (string queue_item in queue.getQueue())
            {
                Functions.QueryParser parsed = Functions.QueryParser.Parse(queue_item);

                // Get the DVD Title
                string title = "";
                if (parsed.DVDTitle == 0)
                    title = "Auto";
                else
                    title = parsed.DVDTitle.ToString();

                // Get the DVD Chapters
                string chapters = "";
                if (parsed.DVDChapterStart == 0)
                    chapters = "Auto";
                else
                {
                    chapters = parsed.DVDChapterStart.ToString();
                    if (parsed.DVDChapterFinish != 0)
                        chapters = chapters + " - " + parsed.DVDChapterFinish;
                }

                ListViewItem item = new ListViewItem();
                item.Text = title; // Title
                item.SubItems.Add(chapters); // Chapters
                item.SubItems.Add(parsed.Source); // Source
                item.SubItems.Add(parsed.Destination); // Destination
                item.SubItems.Add(parsed.VideoEncoder); // Video
                item.SubItems.Add(parsed.AudioEncoder1); // Audio

                list_queue.Items.Add(item);
            }
        }

        // Initializes the encode process
        private void btn_encode_Click(object sender, EventArgs e)
        {
            if (queue.count() != 0)
            {
                lbl_status.Visible = false;
                btn_encode.Enabled = false;
            }
            cancel = false;

            // Start the encode
            try
            {
                if (queue.count() != 0)
                {
                    // Setup or reset some values
                    btn_stop.Visible = true;
                    progressBar.Value = 0;
                    lbl_progressValue.Text = "0 %";
                    progressBar.Step = 100 / queue.count();
                    progressBar.Update();
                    Thread theQ = new Thread(startProc);
                    theQ.Start();
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString());
            }
        }

        // Starts the encoding process
        private void startProc(object state)
        {
            try
            {
                // Run through each item on the queue
                while (queue.count() != 0)
                {
                    string query = queue.getNextItemForEncoding();

                    setEncValue();
                    updateUIElements();

                    hbProc = cliObj.runCli(this, query);

                    hbProc.WaitForExit();
                    hbProc.Close();
                    hbProc.Dispose();
                    hbProc = null;

                    query = "";

                    if (cancel == true)
                    {
                        break;
                    }
                }

                resetQueue();

                // After the encode is done, we may want to shutdown, suspend etc.
                cliObj.afterEncodeAction();
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString());
            }
        }

        // Reset's the window to the default state.
        private void resetQueue()
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new ProgressUpdateHandler(resetQueue));
                    return;

                }
                btn_stop.Visible = false;
                btn_encode.Enabled = true;

                if (cancel == true)
                {
                    lbl_status.Visible = true;
                    lbl_status.Text = "Encode Queue Cancelled!";
                }
                else
                {
                    lbl_status.Visible = true;
                    lbl_status.Text = "Encode Queue Completed!";
                }

                lbl_progressValue.Text = "0 %";
                progressBar.Value = 0;
                progressBar.Update();

                lbl_source.Text = "-";
                lbl_dest.Text = "-";
                lbl_vEnc.Text = "-";
                lbl_aEnc.Text = "-";
                lbl_title.Text = "-";
                lbl_chapt.Text = "-";
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString());
            }
        }

        // Stop's the queue from continuing. 
        private void btn_stop_Click(object sender, EventArgs e)
        {
            cancel = true;
            btn_stop.Visible = false;
            btn_encode.Enabled = true;
            MessageBox.Show("No further items on the queue will start. The current encode process will continue until it is finished. \nClick 'Encode Video' when you wish to continue encoding the queue.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // Updates the progress bar and progress label for a new status.
        private void updateUIElements()
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new ProgressUpdateHandler(updateUIElements));
                    return;
                }

                redrawQueue();

                progressBar.PerformStep();
                lbl_progressValue.Text = string.Format("{0} %", progressBar.Value);
                progressBar.Update();
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString());
            }
        }

        // Set's the information lables about the current encode.
        private void setEncValue()
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new setEncoding(setEncValue));
                }

                // found query is a global varible
                Functions.QueryParser parsed = Functions.QueryParser.Parse(queue.getLastQuery());
                lbl_source.Text = parsed.Source;
                lbl_dest.Text = parsed.Destination;


                if (parsed.DVDTitle == 0)
                    lbl_title.Text = "Auto";
                else
                    lbl_title.Text = parsed.DVDTitle.ToString();

                string chapters = "";
                if (parsed.DVDChapterStart == 0)
                {
                    lbl_chapt.Text = "Auto";
                }
                else
                {
                    chapters = parsed.DVDChapterStart.ToString();
                    if (parsed.DVDChapterFinish != 0)
                        chapters = chapters + " - " + parsed.DVDChapterFinish;
                    lbl_chapt.Text = chapters;
                }

                lbl_vEnc.Text = parsed.VideoEncoder;
                lbl_aEnc.Text = parsed.AudioEncoder1;
            }
            catch (Exception)
            {
                // Do Nothing
            }
        }

        // Move an item up the Queue
        private void btn_up_Click(object sender, EventArgs e)
        {
            if (list_queue.SelectedIndices.Count != 0)
            {
                queue.moveUp(list_queue.SelectedIndices[0]);
                redrawQueue();
            }
        }

        // Move an item down the Queue
        private void btn_down_Click(object sender, EventArgs e)
        {
            if (list_queue.SelectedIndices.Count != 0)
            {
                queue.moveDown(list_queue.SelectedIndices[0]);
                redrawQueue();
            }
        }

        // Remove an item from the queue
        private void btn_delete_Click(object sender, EventArgs e)
        {
            if (list_queue.SelectedIndices.Count != 0)
            {
                queue.remove(list_queue.SelectedIndices[0]);
                redrawQueue();
            }
        }

        // Generate a batch file script to run the encode seperate of HandBrake
        private void btn_batch_Click(object sender, EventArgs e)
        {
            string queries = "";
            foreach (string query_item in queue.getQueue()) 
            {
                string fullQuery = '"' + Application.StartupPath.ToString() + "\\HandBrakeCLI.exe" + '"' + query_item;

                if (queries == "")
                    queries = queries + fullQuery;
                else
                    queries = queries + " && " + fullQuery;
            }
            string strCmdLine = queries;

            SaveFile.ShowDialog();
            string filename = SaveFile.FileName;

            if (filename != "")
            {
                try
                {
                    // Create a StreamWriter and open the file, Write the batch file query to the file and 
                    // Close the stream
                    StreamWriter line = new StreamWriter(filename);
                    line.WriteLine(strCmdLine);
                    line.Close();

                    MessageBox.Show("Your batch script has been sucessfully saved.", "Status", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                }
                catch (Exception)
                {
                    MessageBox.Show("Unable to write to the file. Please make sure that the location has the correct permissions for file writing.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                }

            }
        }

        // Hide's the window from the users view.
        private void btn_Close_Click(object sender, EventArgs e)
        {
            this.Hide();
        }

        // Hide's the window when the user tries to "x" out of the window instead of closing it.
        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
            base.OnClosing(e);
        }

    }
}