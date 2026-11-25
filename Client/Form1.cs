using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

// Controlli da inserire nel designer (Name esatti):
//   txtUsername  (TextBox)      btnConnect  (Button)
//   lblStato     (Label)        lblGiocatore (Label)
//   lblParola    (Label, font grande)
//   lblTimer     (Label)        txtRisposta (TextBox)
//   btnInvia     (Button)       lstLog      (ListBox)
//   timerRisposta (Timer, Interval = 1000)

namespace GiocoClient
{
    public partial class Form1 : Form
    {
        private Socket sender;
        private string accumulatedData = "";
        private readonly byte[] bytes = new byte[1024];
        private string myId;
        private int secondsLeft;
        private const int TimeLimit = 10;

        public Form1()
        {
            InitializeComponent();

            btnConnect.Click += btnConnect_Click;
            btnInvia.Click += btnInvia_Click;
            timerRisposta.Tick += timerRisposta_Tick;
            txtRisposta.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && btnInvia.Enabled)
                {
                    e.SuppressKeyPress = true;
                    btnInvia_Click(s, e);
                }
            };
            FormClosing += (s, e) => CloseSocket();

            txtUsername.MaxLength = 20;
            txtRisposta.Enabled = false;
            btnInvia.Enabled = false;
            lblTimer.Text = "";
            lblParola.Text = "-";
            lblGiocatore.Text = "";
            lblStato.Text = "Non connesso";
        }

        // ============ UTILITY UI ============

        private void UI(Action a)
        {
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke(a);
            else a();
        }

        private void Log(string text)
        {
            UI(() =>
            {
                lstLog.Items.Add(text);
                lstLog.TopIndex = lstLog.Items.Count - 1;
            });
        }

        // ============ RETE ============

        private void SendCommand(Command command)
        {
            string json = JsonConvert.SerializeObject(command) + "\n";
            byte[] msg = Encoding.UTF8.GetBytes(json);
            int offset = 0;
            while (offset < msg.Length)
            {
                int sent = sender.Send(msg, offset, msg.Length - offset, SocketFlags.None);
                if (sent == 0) throw new SocketException();
                offset += sent;
            }
        }

        private Command ReceiveCommand()
        {
            while (true)
            {
                int nl = accumulatedData.IndexOf('\n');
                if (nl >= 0)
                {
                    string message = accumulatedData.Substring(0, nl).Trim();
                    accumulatedData = accumulatedData.Substring(nl + 1);
                    if (string.IsNullOrEmpty(message)) continue;
                    return JsonConvert.DeserializeObject<Command>(message);
                }

                int n = sender.Receive(bytes);
                if (n == 0) throw new SocketException();
                accumulatedData += Encoding.UTF8.GetString(bytes, 0, n);
            }
        }

        private void CloseSocket()
        {
            try { sender?.Close(); } catch { }
        }

        // ============ CONNESSIONE ============

        private void btnConnect_Click(object s, EventArgs e)
        {
            string username = txtUsername.Text.Trim();
            if (username.Length == 0 || username.Length > 20)
            {
                MessageBox.Show("Nome non valido. Massimo 20 caratteri.");
                return;
            }

            btnConnect.Enabled = false;
            txtUsername.Enabled = false;
            lblStato.Text = "Connessione in corso...";

            // Tutto il networking gira su un thread separato per non bloccare la GUI
            Task.Run(() => ClientLoop(username));
        }

        private void ClientLoop(string username)
        {
            try
            {
                IPAddress ip = IPAddress.Parse("127.0.0.1");
                sender = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                sender.Connect(new IPEndPoint(ip, 5555));
                Log("Connessione al server effettuata.");

                Command welcome = ReceiveCommand();
                if (welcome.CommandName != "WELCOME")
                {
                    Log("Messaggio di benvenuto non valido.");
                    return;
                }
                myId = welcome.ParametersList[0];
                Log("Il tuo ID è: " + myId);

                SendCommand(new Command
                {
                    CommandName = "USERNAME",
                    ParametersList = new List<string> { username }
                });

                Command conf = ReceiveCommand();
                if (conf.CommandName == "ERROR")
                {
                    Log(conf.ParametersList[0]);
                    return;
                }
                if (conf.CommandName != "USERNAME_ACCEPTED")
                {
                    Log("Registrazione non confermata.");
                    return;
                }

                UI(() => lblStato.Text = "Benvenuto " + username + "! Attendi l'inizio della partita...");
                Log("Sei nella sala d'attesa.");

                while (true)
                {
                    Command cmd = ReceiveCommand();
                    HandleCommand(cmd);
                }
            }
            catch (SocketException)
            {
                Log("Connessione al server interrotta.");
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                Log("Errore client: " + ex.Message);
            }
            finally
            {
                CloseSocket();
                UI(() =>
                {
                    timerRisposta.Stop();
                    lblStato.Text = "Disconnesso";
                    txtRisposta.Enabled = false;
                    btnInvia.Enabled = false;
                    btnConnect.Enabled = true;
                    txtUsername.Enabled = true;
                });
            }
        }

        // Eseguito sul thread di rete: ogni accesso ai controlli passa da UI()
        private void HandleCommand(Command cmd)
        {
            switch (cmd.CommandName)
            {
                case "GAME_START":
                case "NEW_TURN":
                {
                    string selId = cmd.ParametersList[0];
                    string parola = cmd.ParametersList[1];
                    string selName = cmd.ParametersList[2];

                    int secs;
                    if (cmd.ParametersList.Count < 4 ||
                        !int.TryParse(cmd.ParametersList[3], out secs))
                        secs = TimeLimit;

                    bool myTurn = selId == myId;

                    Log(cmd.CommandName == "GAME_START"
                        ? "=== PARTITA INIZIATA ==="
                        : "=== NUOVO TURNO ===");
                    Log("Turno di " + selName + " - stringa: " + parola);

                    UI(() =>
                    {
                        lblParola.Text = parola;
                        lblGiocatore.Text = "Turno di: " + selName;

                        lblStato.Text = myTurn
                            ? "TOCCA A TE! Scrivi parole che contengono la stringa."
                            : "Turno di " + selName + ". Attendi...";

                        StartTurnTimer(secs, myTurn);
                    });
                    break;
                }

                case "ANSWER_RECEIVED":
                {
                    string ansId = cmd.ParametersList[0];
                    string who = cmd.ParametersList[1];
                    string answer = cmd.ParametersList[2];
                    string esito = cmd.ParametersList.Count > 3 ? cmd.ParametersList[3] : "";

                    Log(who + ": " + answer +
                        (esito == "VALID" ? "  ✔ valida" : "  ✘ non valida"));

                    if (esito == "VALID")
                    {
                        // Turno concluso: arrivera' il NEW_TURN dell'altro giocatore
                        UI(() =>
                        {
                            timerRisposta.Stop();
                            lblTimer.Text = "";
                            txtRisposta.Enabled = false;
                            btnInvia.Enabled = false;
                        });
                    }
                    break;
                }

                case "GAME_OVER":
                {
                    string loserId = cmd.ParametersList[0];
                    string loserName = cmd.ParametersList[1];

                    Log("=== PARTITA TERMINATA ===");
                    Log("Tempo scaduto per " + loserName + ".");
                    Log("Nuova partita tra pochi secondi...");

                    UI(() =>
                    {
                        timerRisposta.Stop();
                        lblTimer.Text = "";
                        txtRisposta.Enabled = false;
                        btnInvia.Enabled = false;

                        lblStato.Text = loserId == myId
                            ? "TEMPO SCADUTO! Hai perso."
                            : "Tempo scaduto per " + loserName + ". Hai vinto!";
                    });
                    break;
                }

                case "ERROR":
                    Log("ERRORE: " + cmd.ParametersList[0]);
                    break;

                default:
                    Log("Messaggio server: " + cmd.CommandName);
                    break;
            }
        }

        // ============ TURNO + TIMER ============

        // Il conteggio qui e' solo visivo: il tempo vero lo controlla il server
        private void StartTurnTimer(int seconds, bool myTurn)
        {
            secondsLeft = seconds;
            lblTimer.Text = "Tempo rimasto: " + secondsLeft + " s";

            txtRisposta.Clear();
            txtRisposta.Enabled = myTurn;
            btnInvia.Enabled = myTurn;

            if (myTurn)
                txtRisposta.Focus();

            timerRisposta.Stop();
            timerRisposta.Start();
        }

        private void timerRisposta_Tick(object sender_, EventArgs e)
        {
            secondsLeft--;
            lblTimer.Text = "Tempo rimasto: " + Math.Max(secondsLeft, 0) + " s";

            if (secondsLeft <= 0)
            {
                timerRisposta.Stop();
                txtRisposta.Enabled = false;
                btnInvia.Enabled = false;
            }
        }

        private void btnInvia_Click(object s, EventArgs e)
        {
            InviaRisposta(txtRisposta.Text);
        }

        // Tentativi illimitati: il campo resta attivo finche' non scade il tempo
        private void InviaRisposta(string answer)
        {
            answer = answer.Trim();
            if (answer.Length == 0)
                return;

            try
            {
                SendCommand(new Command
                {
                    CommandName = "ANSWER",
                    ParametersList = new List<string> { answer }
                });
            }
            catch (Exception ex)
            {
                Log("Errore invio: " + ex.Message);
            }

            txtRisposta.Clear();
            txtRisposta.Focus();
        }
    }

    public class Command
    {
        public string CommandName { get; set; }
        public List<string> ParametersList { get; set; }
    }
}
