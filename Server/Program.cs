
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using System.IO;
using System.Linq;

public class SynchronousSocketListener
{
    private const int MinPlayersToStart = 2;
    private const int MaxPlayers = 2;
    private const int WaitingTimeSeconds = 10;

    private static readonly object lockObject = new object();
    private static readonly Random random = new Random();

    private static readonly List<Player> connectedClients =
        new List<Player>();

    private static Timer gameTimer;

    private static bool waitingForPlayers = false;
    private static bool gameStarted = false;

    private static int nextPlayerId = 1;
    private static int selectedPlayerId = -1;

    // Turni: ogni giocatore ha TurnTimeSeconds secondi, con tentativi illimitati
    private const int TurnTimeSeconds = 10;

    // true = la parola deve contenere la stringa estratta oltre a stare nel dizionario
    private static readonly bool RequireStringInWord = true;

    private static Timer turnTimer;
    private static int turnCounter = 0;
    private static string currentString = "";

    // Lista delle stringhe disponibili
    private static readonly List<string> parole =
        new List<string>
    {
        "AN", "RE", "TO", "AR", "ST", "AL", "IN", "CO",
        "MA", "PE", "TE", "RI", "CA", "TA", "OR", "EN",
        "LI", "PA", "TI", "VI", "RO", "ME", "DE", "RA",
        "AM", "IL", "EL", "SO", "SI", "NO",
        "ATO", "ERE", "ENT", "ION", "ICA", "AND",
        "EST", "ANT", "ARE", "IRE", "ITA", "ONE",
        "TOR", "TAR", "PER", "CON", "STA", "TRA",
        "PRO", "NON", "PAR", "TER", "MAR", "LIN",
        "MIN", "TIN", "RAT", "SET", "RIT", "TEL",
        "AMEN", "ARIO", "ATTO", "ANZA", "ENZA",
        "MENT", "ABIL", "IBIL", "ATOR", "IZIO",
        "AZIO", "ONAL", "ISTI", "ETTO", "INO",
        "ANTE", "ENTE", "IERO", "ERIA", "ANDO",
        "ENDO", "ARLO", "ERLE", "IACA", "ULOS",
        "ESTA", "ACCH", "ICCH", "ONCH", "URGO",
        "QU", "GI", "CH", "GH", "GL", "GN", "SC",
        "FR", "TR", "GR", "PR", "BR", "CR", "BL",
        "CL", "FL", "PL", "SB", "SP", "MB", "MP",
        "NT", "ND", "NC", "NG", "ZZ", "CC", "TT",
        "LL", "GIO", "SQU", "ALL", "CCO", "RRE",
        "MBO", "GRA", "STR", "CHI", "CHE", "GHI",
        "GHE", "PPO", "TTO", "LLI", "GGA", "FFA",
        "BRO", "CRI", "FOS", "GOL", "LUM", "MAL",
        "PAT", "SAN", "VEN", "ZAN", "SCA", "SCO",
        "SCR", "SQUA", "STRA", "STRI", "STRO",
        "SCHI", "SCHE", "GIAC", "GIOV", "GRAZ",
        "CHIA", "CHIE", "CHIO", "GHIA", "GHIE",
        "BRIC", "CROC", "DRAG", "FRAN", "FRES",
        "FLAM", "PLAS", "TRAN", "TRIC", "VINC",
        "CAMP", "PANT", "TAMP", "BOMB", "MANG",
        "SANG", "HI", "HE", "HA", "HO", "ZA", "ZE",
        "ZO", "ZU", "ZI", "PS", "PN", "TM", "TL",
        "FT", "DV", "BD", "PT", "MN", "SM", "SL",
        "SF", "SV", "VR", "BS", "CS", "XU", "EX",
        "PH", "TH", "RH", "ZIO", "GNA", "SCH",
        "GLI", "SCI", "UOI", "FIE", "BIE", "VIO",
        "GNE", "GNI", "GNO", "GLO", "GLU", "PHE",
        "RCO", "RTI", "LCO", "MPE", "MPI", "NFA",
        "NFE", "NFI", "SBA", "SBE", "SBR", "SFR",
        "ZZA", "ZZE", "ZZI", "GLIE", "GNOL", "GNAV",
        "SCIA", "SCIE", "SCIO", "IUOL", "UOLO",
        "SBRU", "SBLE", "SFRE", "SQUI", "ACQU",
        "ARCH", "OLTR", "OMBR", "INFR", "ESPR",
        "ISTR", "OSTR", "ALCH"
    };

    // =========================================
    // AVVIO SERVER
    // =========================================

    public static void StartListening()
    {
        IPAddress ipAddress = IPAddress.Any;

        IPEndPoint localEndPoint =
            new IPEndPoint(ipAddress, 5555);

        Socket listener = new Socket(
            ipAddress.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp
        );

        try
        {
            listener.Bind(localEndPoint);
            listener.Listen(10);

            Console.WriteLine(
                "Server avviato sulla porta 5555..."
            );

            while (true)
            {
                Socket handler = listener.Accept();

                Player player;

                lock (lockObject)
                {
                    if (connectedClients.Count >= MaxPlayers ||
                        gameStarted)
                    {
                        handler.Close();
                        continue;
                    }

                    player = new Player
                    {
                        Id = nextPlayerId++,
                        Socket = handler
                    };

                    connectedClients.Add(player);

                    Console.WriteLine(
                        "Giocatori connessi: " +
                        connectedClients.Count
                    );

                    // Il timer parte con il primo giocatore
                    if (!waitingForPlayers && !gameStarted)
                    {
                        StartWaitingTimerLocked();
                    }
                }

                Console.WriteLine(
                    "[+] Giocatore connesso: " + player.Id
                );

                try
                {
                    SendMessage(player, new Command
                    {
                        CommandName = "WELCOME",
                        ParametersList = new List<string>
                        {
                            player.Id.ToString()
                        }
                    });
                }
                catch
                {
                    RemoveClient(player);
                    continue;
                }

                ClientManager manager =
                    new ClientManager(player);

                Thread t = new Thread(manager.doClient);
                t.Start();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(
                "Errore server: " + e.Message
            );
        }
        finally
        {
            listener.Close();
        }
    }
    
    // =========================================
    // AVVIO TIMER DI 30 SECONDI
    // =========================================

    private static void StartWaitingTimerLocked()
    {
        waitingForPlayers = true;

        gameTimer?.Dispose();

        Console.WriteLine(
            "Conto alla rovescia iniziato: 30 secondi."
        );

        gameTimer = new Timer(
            StartGameAfterTimeout,
            null,
            WaitingTimeSeconds * 1000,
            Timeout.Infinite
        );
    }

    // =========================================
    // CONTROLLO DOPO 30 SECONDI
    // =========================================

    private static void StartGameAfterTimeout(object state)
    {
        lock (lockObject)
        {
            gameTimer?.Dispose();
            gameTimer = null;

            if (!waitingForPlayers || gameStarted)
                return;

            // Controlla il numero di giocatori
            if (connectedClients.Count < MinPlayersToStart)
            {
                waitingForPlayers = false;

                Console.WriteLine(
                    "Tempo scaduto: giocatori insufficienti."
                );

                return;
            }

            // Controlla che tutti abbiano inserito il nome
            foreach (Player player in connectedClients)
            {
                if (string.IsNullOrWhiteSpace(player.Username))
                {
                    Console.WriteLine(
                        "Nome mancante per il giocatore " +
                        player.Id
                    );

                    // Concede altri 30 secondi
                    StartWaitingTimerLocked();

                    return;
                }
            }

            waitingForPlayers = false;
            gameStarted = true;
        }

        Console.WriteLine(
            "Tempo scaduto! Avvio della partita."
        );

        StartGame();
    }

    // =========================================
    // INIZIO PARTITA
    // =========================================

    public static void StartGame()
    {
        Player firstPlayer;

        lock (lockObject)
        {
            if (connectedClients.Count != 2 ||
                parole.Count == 0 ||
                !gameStarted)
            {
                gameStarted = false;
                return;
            }

            // Scelta casuale del primo giocatore
            firstPlayer = connectedClients[
                random.Next(connectedClients.Count)
            ];

            currentString = "";
        }

        Console.WriteLine("\n==============================");
        Console.WriteLine("PARTITA INIZIATA!");
        Console.WriteLine("==============================\n");

        StartTurn(firstPlayer, "GAME_START");
    }

    // =========================================
    // NUOVO TURNO
    // =========================================

    // commandName: "GAME_START" per il primo turno, "NEW_TURN" per i successivi
    private static void StartTurn(Player player, string commandName)
    {
        string word;
        int turnId;

        lock (lockObject)
        {
            // Stringa diversa da quella del turno precedente
            do
            {
                word = parole[random.Next(parole.Count)];
            }
            while (parole.Count > 1 && word == currentString);

            currentString = word;
            selectedPlayerId = player.Id;

            turnId = ++turnCounter;

            turnTimer?.Dispose();
            turnTimer = new Timer(
                TurnTimeout,
                turnId,
                TurnTimeSeconds * 1000,
                Timeout.Infinite
            );
        }

        Console.WriteLine(
            "Turno di " + player.Username +
            " (ID " + player.Id + ") - stringa: " + word
        );

        BroadcastMessage(new Command
        {
            CommandName = commandName,
            ParametersList = new List<string>
            {
                player.Id.ToString(),
                word,
                player.Username,
                TurnTimeSeconds.ToString()
            }
        });
    }

    // =========================================
    // TEMPO SCADUTO PER IL TURNO
    // =========================================

    private static void TurnTimeout(object state)
    {
        int turnId = (int)state;
        Player loser;

        lock (lockObject)
        {
            // Turno gia' concluso o partita interrotta
            if (!gameStarted || turnId != turnCounter)
                return;

            loser = connectedClients.FirstOrDefault(
                p => p.Id == selectedPlayerId
            );

            gameStarted = false;
            selectedPlayerId = -1;

            turnTimer?.Dispose();
            turnTimer = null;
        }

        if (loser == null)
            return;

        Console.WriteLine(
            "TEMPO SCADUTO per " + loser.Username +
            ": partita terminata."
        );

        BroadcastMessage(new Command
        {
            CommandName = "GAME_OVER",
            ParametersList = new List<string>
            {
                loser.Id.ToString(),
                loser.Username,
                "TIMEOUT"
            }
        });

        // Nuova partita automatica tra WaitingTimeSeconds secondi
        lock (lockObject)
        {
            if (connectedClients.Count > 0 && !waitingForPlayers)
            {
                StartWaitingTimerLocked();
            }
        }
    }

    // =========================================
    // INVIO MESSAGGIO A UN GIOCATORE
    // =========================================

    public static void SendMessage(
        Player player,
        Command command)
    {
        string json =
            JsonConvert.SerializeObject(command) + "\n";

        byte[] data = Encoding.UTF8.GetBytes(json);

        lock (player.SendLock)
        {
            int offset = 0;

            while (offset < data.Length)
            {
                int sent = player.Socket.Send(
                    data,
                    offset,
                    data.Length - offset,
                    SocketFlags.None
                );

                if (sent == 0)
                    throw new SocketException();

                offset += sent;
            }
        }
    }

    // =========================================
    // INVIO MESSAGGIO A TUTTI
    // =========================================

    public static void BroadcastMessage(Command command)
    {
        Player[] players;

        lock (lockObject)
        {
            players = connectedClients.ToArray();
        }

        foreach (Player player in players)
        {
            try
            {
                SendMessage(player, command);
            }
            catch (Exception e)
            {
                Console.WriteLine(
                    "Errore invio giocatore " +
                    player.Id + ": " + e.Message
                );

                RemoveClient(player);
            }
        }
    }

    // =========================================
    // GESTIONE COMANDI RICEVUTI
    // =========================================

    public static void ProcessCommand(
        Player player,
        Command cmd)
    {
        if (cmd == null || cmd.CommandName == null)
            return;

        switch (cmd.CommandName.ToUpper())
        {
            // ---------------------------------
            // REGISTRAZIONE USERNAME
            // ---------------------------------

            case "USERNAME":

                string username =
                    cmd.ParametersList != null &&
                    cmd.ParametersList.Count > 0
                    ? cmd.ParametersList[0].Trim()
                    : "";

                if (string.IsNullOrWhiteSpace(username) ||
                    username.Length > 20)
                {
                    SendMessage(player, new Command
                    {
                        CommandName = "ERROR",
                        ParametersList = new List<string>
                        {
                            "Nome non valido. Massimo 20 caratteri."
                        }
                    });

                    return;
                }

                lock (lockObject)
                {
                    if (!connectedClients.Contains(player))
                        return;

                    if (gameStarted)
                    {
                        SendMessage(player, new Command
                        {
                            CommandName = "ERROR",
                            ParametersList = new List<string>
                            {
                                "La partita è già iniziata."
                            }
                        });

                        return;
                    }

                    player.Username = username;
                }

                Console.WriteLine(
                    "Nome registrato: " +
                    username +
                    " (ID: " + player.Id + ")"
                );

                // Conferma al giocatore
                SendMessage(player, new Command
                {
                    CommandName = "USERNAME_ACCEPTED",
                    ParametersList = new List<string>
                    {
                        username
                    }
                });

                break;

            // ---------------------------------
            // RISPOSTA DEL GIOCATORE
            // ---------------------------------

            case "ANSWER":
            {
                string answer =
                    cmd.ParametersList != null &&
                    cmd.ParametersList.Count > 0
                    ? cmd.ParametersList[0].Trim().ToUpper()
                    : "";

                bool authorized;
                bool valid = false;
                string currentWord;
                Player nextPlayer = null;

                lock (lockObject)
                {
                    authorized =
                        gameStarted &&
                        player.Id == selectedPlayerId &&
                        connectedClients.Contains(player);

                    currentWord = currentString;

                    if (authorized && answer.Length > 0)
                    {
                        valid =
                            IsValidWord(answer) &&
                            (!RequireStringInWord ||
                             answer.Contains(currentWord));

                        if (valid)
                        {
                            // Chiude subito il turno: nessun'altra
                            // risposta o timeout puo' piu' contare
                            selectedPlayerId = -1;
                            turnCounter++;

                            turnTimer?.Dispose();
                            turnTimer = null;

                            nextPlayer = connectedClients.FirstOrDefault(
                                p => p.Id != player.Id
                            );
                        }
                    }
                }

                if (!authorized)
                {
                    SendMessage(player, new Command
                    {
                        CommandName = "ERROR",
                        ParametersList = new List<string>
                        {
                            "Non e' il tuo turno!"
                        }
                    });

                    return;
                }

                if (answer.Length == 0)
                    return;

                Console.WriteLine(
                    (valid ? "Parola valida: " : "Parola NON valida: ") +
                    answer + " (stringa " + currentWord + ", " +
                    player.Username + ")"
                );

                // Esito del tentativo a entrambi i giocatori
                BroadcastMessage(new Command
                {
                    CommandName = "ANSWER_RECEIVED",
                    ParametersList = new List<string>
                    {
                        player.Id.ToString(),
                        player.Username,
                        answer,
                        valid ? "VALID" : "INVALID"
                    }
                });

                // Parola valida: il turno passa all'altro giocatore
                if (valid && nextPlayer != null)
                {
                    StartTurn(nextPlayer, "NEW_TURN");
                }

                break;
            }

            // ---------------------------------
            // DISCONNESSIONE
            // ---------------------------------

            case "QUIT":

                Console.WriteLine(
                    "Il giocatore " +
                    player.Username +
                    " ha richiesto la disconnessione."
                );

                RemoveClient(player);

                break;

            // ---------------------------------
            // COMANDO NON RICONOSCIUTO
            // ---------------------------------

            default:

                Console.WriteLine(
                    "Comando sconosciuto: " +
                    cmd.CommandName
                );

                break;
        }
    }

    // =========================================
    // RIMOZIONE CLIENT
    // =========================================

    public static void RemoveClient(Player player)
    {
        bool removed;

        lock (lockObject)
        {
            removed = connectedClients.Remove(player);

            if (removed)
            {
                if (gameStarted)
                {
                    gameStarted = false;
                    selectedPlayerId = -1;

                    turnTimer?.Dispose();
                    turnTimer = null;

                    Console.WriteLine(
                        "Partita interrotta per disconnessione."
                    );

                    if (connectedClients.Count > 0)
                    {
                        StartWaitingTimerLocked();
                    }
                }
                else if (connectedClients.Count == 0)
                {
                    waitingForPlayers = false;

                    gameTimer?.Dispose();
                    gameTimer = null;
                }
            }
        }

        if (!removed)
            return;

        Console.WriteLine(
            "Giocatore disconnesso: " + player.Id
        );

        try
        {
            player.Socket.Shutdown(SocketShutdown.Both);
        }
        catch { }

        try
        {
            player.Socket.Close();
        }
        catch { }
    }

    // =========================================
    // MAIN
    // =========================================

    public static int Main(string[] args)
    {
        if (!LoadDictionary())
        {
            Console.WriteLine();
            Console.WriteLine("Premere INVIO per uscire.");
            Console.ReadLine();
            return 1;
        }

        StartListening();

        return 0;
    }

    // =========================================
    // DIZIONARIO
    // =========================================

    private const string DictionaryFileName =
        "280000_parole_italiane.txt";

    private static HashSet<string> dizionario;

    // Cerca il file nella cartella corrente e accanto all'eseguibile
    private static bool LoadDictionary()
    {
        string[] candidates =
        {
            Path.Combine(Directory.GetCurrentDirectory(), DictionaryFileName),
            Path.Combine(AppContext.BaseDirectory, DictionaryFileName)
        };

        foreach (string path in candidates)
        {
            if (!File.Exists(path))
                continue;

            dizionario = new HashSet<string>(
                File.ReadAllLines(path)
                    .Select(p => p.Trim().ToUpper())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
            );

            Console.WriteLine(
                "Dizionario caricato: " + dizionario.Count +
                " parole (" + path + ")"
            );

            return true;
        }

        Console.WriteLine(
            "ERRORE: file '" + DictionaryFileName + "' non trovato."
        );
        Console.WriteLine("Cercato in:");
        foreach (string path in candidates)
            Console.WriteLine("  " + path);
        Console.WriteLine(
            "Copia il file in una di queste cartelle e riavvia il server."
        );

        return false;
    }

    private static bool IsValidWord(string word)
    {
        return dizionario != null &&
               dizionario.Contains(word.Trim().ToUpper());
    }
}
public class Player
{
    public int Id { get; set; }

    public string Username { get; set; }

    public Socket Socket { get; set; }

    public object SendLock { get; } = new object();
}

public class ClientManager
{
    private Player player;

    private byte[] buffer = new byte[1024];

    public ClientManager(Player player)
    {
        this.player = player;
    }

    public void doClient()
    {
        string accumulatedData = "";

        try
        {
            while (true)
            {
                int bytesRec =
                    player.Socket.Receive(buffer);

                if (bytesRec == 0)
                    break;

                accumulatedData += Encoding.UTF8.GetString(
                    buffer,
                    0,
                    bytesRec
                );

                int newlineIndex;

                while ((newlineIndex =
                    accumulatedData.IndexOf('\n')) >= 0)
                {
                    string singleMessage =
                        accumulatedData.Substring(
                            0,
                            newlineIndex
                        ).Trim();

                    accumulatedData =
                        accumulatedData.Substring(
                            newlineIndex + 1
                        );

                    if (string.IsNullOrEmpty(singleMessage))
                        continue;

                    Command cmd = null;

                    try
                    {
                        cmd =
                            JsonConvert.DeserializeObject<Command>(
                                singleMessage
                            );
                    }
                    catch (JsonException)
                    {
                        Console.WriteLine(
                            "JSON non valido ricevuto dal giocatore " +
                            player.Id
                        );

                        continue;
                    }

                    SynchronousSocketListener.ProcessCommand(
                        player,
                        cmd
                    );
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(
                "Errore client " +
                player.Id +
                ": " + e.Message
            );
        }
        finally
        {
            SynchronousSocketListener.RemoveClient(player);
        }
    }
}

public class Command
{
    public string CommandName { get; set; }

    public List<string> ParametersList { get; set; }
}
