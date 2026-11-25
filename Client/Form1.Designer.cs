using System.Drawing;
using System.Windows.Forms;

namespace GiocoClient
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        private TextBox txtUsername;
        private Button btnConnect;
        private Label lblStato;
        private Label lblGiocatore;
        private Label lblParola;
        private Label lblTimer;
        private TextBox txtRisposta;
        private Button btnInvia;
        private ListBox lstLog;
        private Timer timerRisposta;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            txtUsername = new TextBox();
            btnConnect = new Button();
            lblStato = new Label();
            lblGiocatore = new Label();
            lblParola = new Label();
            lblTimer = new Label();
            txtRisposta = new TextBox();
            btnInvia = new Button();
            lstLog = new ListBox();
            timerRisposta = new Timer(components);

            SuspendLayout();

            // txtUsername
            txtUsername.Name = "txtUsername";
            txtUsername.Location = new Point(15, 15);
            txtUsername.Size = new Size(300, 23);

            // btnConnect
            btnConnect.Name = "btnConnect";
            btnConnect.Text = "Connetti";
            btnConnect.Location = new Point(325, 14);
            btnConnect.Size = new Size(100, 25);

            // lblStato
            lblStato.Name = "lblStato";
            lblStato.Location = new Point(15, 50);
            lblStato.Size = new Size(490, 20);

            // lblGiocatore
            lblGiocatore.Name = "lblGiocatore";
            lblGiocatore.Location = new Point(15, 75);
            lblGiocatore.Size = new Size(490, 20);

            // lblParola
            lblParola.Name = "lblParola";
            lblParola.Font = new Font("Segoe UI", 28F, FontStyle.Bold);
            lblParola.TextAlign = ContentAlignment.MiddleCenter;
            lblParola.Location = new Point(15, 105);
            lblParola.Size = new Size(490, 60);

            // lblTimer
            lblTimer.Name = "lblTimer";
            lblTimer.Location = new Point(15, 175);
            lblTimer.Size = new Size(490, 20);

            // txtRisposta
            txtRisposta.Name = "txtRisposta";
            txtRisposta.Location = new Point(15, 200);
            txtRisposta.Size = new Size(400, 23);

            // btnInvia
            btnInvia.Name = "btnInvia";
            btnInvia.Text = "Invia";
            btnInvia.Location = new Point(425, 199);
            btnInvia.Size = new Size(80, 25);

            // lstLog
            lstLog.Name = "lstLog";
            lstLog.Location = new Point(15, 240);
            lstLog.Size = new Size(490, 230);

            // timerRisposta
            timerRisposta.Interval = 1000;

            // Form1
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(520, 485);
            Text = "Gioco Multiplayer";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            Controls.Add(txtUsername);
            Controls.Add(btnConnect);
            Controls.Add(lblStato);
            Controls.Add(lblGiocatore);
            Controls.Add(lblParola);
            Controls.Add(lblTimer);
            Controls.Add(txtRisposta);
            Controls.Add(btnInvia);
            Controls.Add(lstLog);

            ResumeLayout(false);
            PerformLayout();
        }
    }
}
