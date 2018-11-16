using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;           // per IPEndPoint, IPAddress, Dns
using System.Net.Sockets;   // per sockets UDP
using Microsoft.VisualBasic;

namespace ScacchiUDP
{
    public partial class FormScacchi : System.Windows.Forms.Form
    {
        int intUdpLocalPort;
        int intUdpRemotePort;
        string strIpRemote;

        Socket udpSocket;               // socket per ricevere e trasmettere
        EndPoint ep;                    // l'endpoint dell'altro capo (sia in ricezione e in spedizione) da usare con le routine asincrone di C#
        byte[] abytRx = new byte[1024]; // il buffer di ricezione
        byte[] abytTx = new byte[1024]; // il buffer di spedizione

        List<Label> lbl;

        IPAddress localIp;

        string selectedPawn = "";
        Label lastLblClicked;

        //logica
        bool white;
        bool yourTurn;

        public FormScacchi(int intUdpLocalPort, int intUdpRemotePort, string strIpRemote)
        {
            Application.ApplicationExit += new EventHandler(this.OnApplicationExit);
            InitializeComponent();
            this.intUdpLocalPort = intUdpLocalPort;
            this.intUdpRemotePort = intUdpRemotePort;
            this.strIpRemote = strIpRemote;

            lbl = Controls.OfType<Label>().ToList();

            if (udpSocket != null)
            {
                udpSocket.Shutdown(SocketShutdown.Both);
                udpSocket.Close();
                udpSocket = null;
            }

            IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress addr in localIPs)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIp = addr;
                }
            }
            gameInit();
            Bind();
        }

        private void Bind()
        {
            try
            {
                udpSocket = null;
                IPEndPoint ipEP; // ???????
                // Creazione socket Udp (con set del tipo di socket e di protocollo utilizzato)
                udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                // L'endpoint locale per la ricezione
                ipEP = new IPEndPoint(IPAddress.Any, intUdpLocalPort);
                // Associazione degli indirizzi al socket (per la ricezione): IP locale e Porta Locale
                udpSocket.Bind(ipEP);
                ep = (EndPoint)ipEP;
                // Impostazione della ricezione asincrona sul socket
                udpSocket.BeginReceiveFrom(abytRx, 0, abytRx.Length, SocketFlags.None, ref ep, new AsyncCallback(OnReceive), ep);
                MessageBox.Show("Pronto a ricevere sulla porta locale " + intUdpLocalPort.ToString(), "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (SocketException ex)
            {
                MessageBox.Show("Bind(): eccezione SocketException\n" + ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Bind(): eccezione Exception\n" + ex.Message);
            }

        }

        private void Send(string strMessage)
        {
            // Considero l'indirizzo Ip selezionato
            if (strIpRemote != "" && strIpRemote != null)
            {
                IPEndPoint ipEP;
                IPAddress ipAddress;

                // Ecco l'IPaddress dalla stringa con l'indirizzo IP
                ipAddress = IPAddress.Parse(strIpRemote);
                // L'endpoint remoto a cui spedire
                ipEP = new IPEndPoint(ipAddress, intUdpRemotePort);
                ep = (EndPoint)ipEP;

                try
                {
                    abytTx = Encoding.UTF8.GetBytes(strMessage);

                    // Spedizione asincrona del buffer di byte
                    udpSocket.BeginSendTo(abytTx, 0, strMessage.Length, SocketFlags.None, ep, new AsyncCallback(OnSend), null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Send(): eccezione Exception\n" + ex.Message);
                }
            }
            else
            {
                MessageBox.Show("Indirizzo Ip destinazione mancante");
            }
        }

        private delegate void del_OnReceive(IAsyncResult ar);

        private void OnReceive(IAsyncResult ar)
        {
            if (InvokeRequired) // Per gestire il crossthread (questa routine è chiamata da un altro thread)
            {
                BeginInvoke(new del_OnReceive(OnReceive), ar);
                return;
            }

            try
            {
                string strReceived;
                int idx;
                IPEndPoint ipEPRx;

                if (udpSocket == null)
                {
                    MessageBox.Show("Socket Nullo", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                ipEPRx = new IPEndPoint(IPAddress.Any, 0);
                ep = (EndPoint)ipEPRx;
                // Ecco la fine della ricezione. Ora i dati ricevuti sono nel buffer globale
                udpSocket.EndReceiveFrom(ar, ref ep);

                // Recupero Ip e Porta dell'host remoto
                string[] astr = ep.ToString().Split(':');
                // Ecco il messaggio ricevuto.
                strReceived = Encoding.UTF8.GetString(abytRx); // trasformo in inga i dati ricevuti

                // Prendo solo i caratteri che precedono il carattere nullo (il tipo ing non è come l'array di char del C
                idx = strReceived.IndexOf((char)0);

                if (idx > -1)
                {
                    strReceived = strReceived.Substring(0, idx);
                }
                // -------------------------------------------------------------------------
                //lst.Items.Insert(0, "<IP Remote: " + astr[0] + ", Remote Port: " + astr[1] + ">" + strReceived); // Sul listbox
                if (strReceived == "poke")
                {
                    MessageBox.Show("Il tuo avversario è inquieto!", "Poke dall'avversario", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (strReceived.Substring(0, 3) == "msg")
                {
                    MessageBox.Show("Il tuo avversario dice: \n" + strReceived.Substring(3, strReceived.Length - 3), "Messaggio dall'avversario", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (strReceived == "exit")
                {
                    MessageBox.Show("Il tuo avversario ha abbandonato la partita.", "Disconnessione dell'avversario", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Application.Exit();
                }
                else
                {
                    refresh(strReceived);
                }

                // -------------------------------------------------------------------------

                // Reinizializzo il buffer con zeri, per evitare che la prossima ricezione sovrapponga la precedente
                abytRx = new byte[abytRx.Length];
                // Riassocio la routine di ricezione
                udpSocket.BeginReceiveFrom(abytRx, 0, abytRx.Length, SocketFlags.None, ref ep, new AsyncCallback(OnReceive), ep);
            }
            catch (ObjectDisposedException ex)
            {
                MessageBox.Show("OnReceive(): Eccezione ObjectDisposedException\n" + ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show("OnReceive(): Eccezione Exception\n" + ex.Message);
            }
        }

        private delegate void del_OnSend(IAsyncResult ar);
        private void OnSend(IAsyncResult ar)
        {
            if (InvokeRequired) // Per gestire il crossthread (questa routine è chiamata da un altro thread)
            {
                BeginInvoke(new del_OnSend(OnSend), ar);
                return;
            }

            try
            {
                udpSocket.EndSend(ar);
            }
            catch (ObjectDisposedException ex)
            {
                MessageBox.Show("OnSend(): Eccezione ObjectDisposedException\n" + ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show("OnSend(): Eccezione Exception\n" + ex.Message);
            }
        }


        //=====================================LOGICA=====================================

        /*
         * Gestione turni:
         * Chi ha l'ultimo ottetto più basso (bianco)
         * 
         * lblA1 -> Scuro
         * lblA1 -> Bianco
         * 
         * Chia ha l'ultimo ottetto più alto (nero)
         * 
         * lblA1 -> Chiaro
         * lblA2 -> Nero
         * 
         * 
         */

        private void gameInit()
        {
            string[] splittedLocalIp = localIp.ToString().Split('.');
            string[] splittedRemoteIp = strIpRemote.ToString().Split('.');

            if (Convert.ToInt16(splittedLocalIp[3]) > Convert.ToInt16(splittedRemoteIp[3]))
            {
                white = true;
                yourTurn = true;
            }
            else
            {
                white = false;
                yourTurn = false;
            }

            if (!white)
            {
                string temp = "";
                for (int i = 1; i <= 2; i++)
                {
                    for (int j = 1; j <= 8; j++)
                    {
                        Label lw = (Label)Controls.Find("lbl" + i + j, true)[0];
                        Label lb;
                        if (i == 1)
                        {
                            lb = (Label)Controls.Find("lbl" + 8 + j, true)[0];
                        }
                        else
                        {
                            lb = (Label)Controls.Find("lbl" + 7 + j, true)[0];
                        }

                        temp = lw.Text;
                        lw.Text = lb.Text;
                        lb.Text = temp;
                    }
                }
                Label lq = (Label)Controls.Find("lbl84", true)[0];
                temp = lq.Text;
                Label lk = (Label)Controls.Find("lbl85", true)[0];
                lq.Text = lk.Text;
                lk.Text = temp;

                lq = (Label)Controls.Find("lbl14", true)[0];
                temp = lq.Text;
                lk = (Label)Controls.Find("lbl15", true)[0];
                lq.Text = lk.Text;
                lk.Text = temp;

            }
        }

        private void lbl_Click(object sender, System.EventArgs e)
        {
            if (yourTurn)
            {
                List<Control> lbl = Controls.OfType<Label>().Cast<Control>().ToList();
                foreach (Label l in lbl)
                {
                    if (((Label)sender) == l)
                    {
                        l.BorderStyle = BorderStyle.FixedSingle;

                        //SELEZIONAMENTO PEDINA
                        if (l.Text != "" && selectedPawn == "")
                        {
                            if (white)
                            {
                                if (Convert.ToChar(l.Text) >= 9812 && Convert.ToChar(l.Text) <= 9817)
                                {
                                    selectedPawn = l.Text != "" ? l.Text : "";
                                    lastLblClicked = l;
                                }
                            }
                            else
                            {
                                if (Convert.ToChar(l.Text) >= 9818 && Convert.ToChar(l.Text) <= 9823)
                                {
                                    selectedPawn = l.Text != "" ? l.Text : "";
                                    lastLblClicked = l;
                                }
                            }
                        }
                        //SPOSTAMENTO PEDINA
                        else if (selectedPawn != "")
                        {
                            if (l.Text != "")
                            {
                                if (white && (Convert.ToChar(l.Text) >= 9812 && Convert.ToChar(l.Text) <= 9817))
                                {
                                    selectedPawn = l.Text != "" ? l.Text : "";
                                    lastLblClicked = l;
                                }
                                else if (!white && (Convert.ToChar(l.Text) >= 9818 && Convert.ToChar(l.Text) <= 9823))
                                {
                                    selectedPawn = l.Text != "" ? l.Text : "";
                                    lastLblClicked = l;
                                }
                            }
                            if (checkMovement(lastLblClicked, l))
                            {
                                //Solo spostamento
                                if (l.Text == "")
                                {
                                    l.Text = lastLblClicked.Text;
                                    lastLblClicked.Text = "";
                                    selectedPawn = "";
                                    yourTurn = false;
                                    Send(lastLblClicked.Name + l.Name);
                                    l.BorderStyle = BorderStyle.None;
                                }
                                //Se bianco mangia nero
                                else if (white && (Convert.ToChar(l.Text) >= 9818 && Convert.ToChar(l.Text) <= 9823))
                                {
                                    l.Text = lastLblClicked.Text;
                                    lastLblClicked.Text = "";
                                    selectedPawn = "";
                                    yourTurn = false;
                                    Send(lastLblClicked.Name + l.Name);
                                    l.BorderStyle = BorderStyle.None;
                                }
                                //se nero mangia bianco
                                else if (!white && (Convert.ToChar(l.Text) >= 9812 && Convert.ToChar(l.Text) <= 9817))
                                {
                                    l.Text = lastLblClicked.Text;
                                    lastLblClicked.Text = "";
                                    selectedPawn = "";
                                    yourTurn = false;
                                    Send(lastLblClicked.Name + l.Name);
                                    l.BorderStyle = BorderStyle.None;
                                }
                            }
                        }
                    }
                    else
                    {
                        l.BorderStyle = BorderStyle.None;
                    }
                }
            }
        }

        private bool checkMovement(Label initL, Label finalL)
        {
            //PEDONE (troppo complesso)
            if (Convert.ToChar(initL.Text) == 9817 || Convert.ToChar(initL.Text) == 9823)
            {
                int intInitPos = Convert.ToInt16(initL.Name.Substring(5 - 2));
                int intFinalPos = Convert.ToInt16(finalL.Name.Substring(5 - 2));
                if (intFinalPos == intInitPos + 10)
                {
                    return true;
                }
                else if (intInitPos - 20 <= 8 && intFinalPos == intInitPos + 20)
                {
                    return true;
                }
                else if (intFinalPos == intInitPos + 9 || intFinalPos == intInitPos + 11)
                {
                    Label temp = (Label)Controls.Find("lbl" + intFinalPos, true)[0];
                    return temp.Text != "" ? true : false;
                }
                else
                {
                    return false;
                }
            }
            //TORRE
            else if (Convert.ToChar(initL.Text) == 9814 || Convert.ToChar(initL.Text) == 9820)
            {
                int intInitPos = Convert.ToInt16(initL.Name.Substring(5 - 2));
                int intFinalPos = Convert.ToInt16(finalL.Name.Substring(5 - 2));
                if ((intFinalPos - intInitPos) % 10 == 0)
                {
                    for (int j = (intFinalPos > intInitPos ? intInitPos : intFinalPos) + 10; j < (intFinalPos > intInitPos ? intFinalPos : intInitPos); j = (intFinalPos > intInitPos ? j + 10 : j - 10))
                    {
                        Label l = (Label)Controls.Find("lbl" + j, true)[0];
                        if (l.Text != "")
                        {
                            return false;
                        }
                    }
                    return true;
                }
                else if (Convert.ToString(intFinalPos).Substring(0, 1) == Convert.ToString(intInitPos).Substring(0, 1))
                {
                    for (int j = (intFinalPos > intInitPos ? intFinalPos : intInitPos) + 1; j < (intFinalPos > intInitPos ? intInitPos : intFinalPos); j = (intFinalPos > intInitPos ? j-- : j++))
                    {
                        Label l = (Label)Controls.Find("lbl" + j, true)[0];
                        if (l.Text != "")
                        {
                            return false;
                        }
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
            //ALFIERE
            else if (Convert.ToChar(initL.Text) == 9815 || Convert.ToChar(initL.Text) == 9821)
            {
                int intInitPos = Convert.ToInt16(initL.Name.Substring(5 - 2));
                int intFinalPos = Convert.ToInt16(finalL.Name.Substring(5 - 2));
                int mIntFinalPos11 = intFinalPos;
                int lIntFinalPos11 = intFinalPos;
                int mIntFinalPos9 = intFinalPos;
                int lIntFinalPos9 = intFinalPos;

                for (int i = 0; i <= 8; i++)
                {
                    if ((mIntFinalPos11 - 11 == intInitPos || lIntFinalPos11 + 11 == intInitPos) || (mIntFinalPos9 - 9 == intInitPos || lIntFinalPos9 + 9 == intInitPos))
                    {
                        return true;
                    }
                    else
                    {
                        mIntFinalPos11 = mIntFinalPos11 + 11;
                        lIntFinalPos11 = lIntFinalPos11 - 11;
                        mIntFinalPos9 = mIntFinalPos9 + 9;
                        lIntFinalPos9 = lIntFinalPos9 - 9;
                    }
                }

                return false;
            }
            //CAVALLO
            else if (Convert.ToChar(initL.Text) == 9816 || Convert.ToChar(initL.Text) == 9822)
            {
                int intInitPos = Convert.ToInt16(initL.Name.Substring(5 - 2));
                int intFinalPos = Convert.ToInt16(finalL.Name.Substring(5 - 2));

                if (intFinalPos - intInitPos == 8 || intFinalPos - intInitPos == -8) return true;
                else if (intFinalPos - intInitPos == 19 || intFinalPos - intInitPos == -19) return true;
                else if (intFinalPos - intInitPos == 21 || intFinalPos - intInitPos == -21) return true;
                else if (intFinalPos - intInitPos == 12 || intFinalPos - intInitPos == -12) return true;
                else return false;
            }
            //RE
            else if (Convert.ToChar(initL.Text) == 9812 || Convert.ToChar(initL.Text) == 9818)
            {
                int intInitPos = Convert.ToInt16(initL.Name.Substring(5 - 2));
                int intFinalPos = Convert.ToInt16(finalL.Name.Substring(5 - 2));

                if (intFinalPos - intInitPos <= 11 && intFinalPos - intInitPos >= -11)
                {
                    return true;
                }
                else return false;
            }
            //REGINA
            else if (Convert.ToChar(initL.Text) == 9813 || Convert.ToChar(initL.Text) == 9819)
            {
                int intInitPos = Convert.ToInt16(initL.Name.Substring(5 - 2));
                int intFinalPos = Convert.ToInt16(finalL.Name.Substring(5 - 2));

                if ((intFinalPos - intInitPos) % 10 == 0)
                {
                    for (int j = (intFinalPos > intInitPos ? intInitPos : intFinalPos) + 10; j < (intFinalPos > intInitPos ? intFinalPos : intInitPos); j = (intFinalPos > intInitPos ? j + 10 : j - 10))
                    {
                        Label l = (Label)Controls.Find("lbl" + j, true)[0];
                        if (l.Text != "")
                        {
                            return false;
                        }
                    }
                    return true;
                }
                else if (Convert.ToString(intFinalPos).Substring(0, 1) == Convert.ToString(intInitPos).Substring(0, 1))
                {
                    for (int j = (intFinalPos > intInitPos ? intFinalPos : intInitPos) + 1; j < (intFinalPos > intInitPos ? intInitPos : intFinalPos); j = (intFinalPos > intInitPos ? j-- : j++))
                    {
                        Label l = (Label)Controls.Find("lbl" + j, true)[0];
                        if (l.Text != "")
                        {
                            return false;
                        }
                    }
                    return true;
                }

                int mIntFinalPos11 = intFinalPos;
                int lIntFinalPos11 = intFinalPos;
                int mIntFinalPos9 = intFinalPos;
                int lIntFinalPos9 = intFinalPos;

                for (int i = 0; i <= 8; i++)
                {
                    if ((mIntFinalPos11 - 11 == intInitPos || lIntFinalPos11 + 11 == intInitPos) || (mIntFinalPos9 - 9 == intInitPos || lIntFinalPos9 + 9 == intInitPos))
                    {
                        return true;
                    }
                    else
                    {
                        mIntFinalPos11 = mIntFinalPos11 + 11;
                        lIntFinalPos11 = lIntFinalPos11 - 11;
                        mIntFinalPos9 = mIntFinalPos9 + 9;
                        lIntFinalPos9 = lIntFinalPos9 - 9;
                    }
                }

                return false;

            }

            //-----
            else
            {
                return true;
            }
        }

        private void refresh(string strReceived)
        {
            string initPos = strReceived.Substring(0, 5);
            string finalPos = strReceived.Substring(10 - 5);

            int intInitPos = Convert.ToInt16(initPos.Substring(5 - 2));
            int intFinalPos = Convert.ToInt16(finalPos.Substring(5 - 2));

            string[] splittedInitPos = new string[2];
            splittedInitPos[0] = Convert.ToString(intInitPos).Substring(0, 1);
            splittedInitPos[1] = Convert.ToString(intInitPos).Substring(2 - 1);

            string[] splittedFinalPos = new string[2];
            splittedFinalPos[0] = Convert.ToString(intFinalPos).Substring(0, 1);
            splittedFinalPos[1] = Convert.ToString(intFinalPos).Substring(2 - 1);

            Label initLabel = (Label)Controls.Find("lbl" + (9 - (Convert.ToInt16(splittedInitPos[0]))) + (9 - (Convert.ToInt16(splittedInitPos[1]))), true)[0];
            Label finalLabel = (Label)Controls.Find("lbl" + (9 - (Convert.ToInt16(splittedFinalPos[0]))) + (9 - (Convert.ToInt16(splittedFinalPos[1]))), true)[0];

            finalLabel.Text = initLabel.Text;
            initLabel.Text = "";
            yourTurn = true;
        }

        private void inviaPokeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Send("poke");
        }

        private void inviaMessaggioToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string message = Interaction.InputBox("Scrivi il messaggio per l'avversario:", "Messaggio", "");
            if (message == "") return;
            else Send("msg" + message);
        }

        private void problemaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Chiama Filippo Scaramuzza, lui ne sa a pacchi.");
        }

        private void comeSiMuovonoLePedineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Movimenti Pedine: \n\n" +
                            "-Pedone: \n" +
                            "\tSe la prima volta che si muove, può spostarsi di 2 caselle in avanti,\n" +
                            "\taltrimenti sempre 1. Per mangiare si muove di 1 in diagonale.\n" +
                            "-Torre: \n" +
                            "\tSi muove solo sull'asse verticale e orizzontale di \n\ttutte le caselle che vuole.\n" +
                            "-Cavallo: \n" +
                            "\tE' l'unico che può 'saltare' le altre pedine. Si muove a \n\tforma di <L>.\n" +
                            "-Alfiere: \n" +
                            "\tSi muove in diagonale per tutte le caselle che vuole.\n" +
                            "-Re: \n" +
                            "\tSi muove in tutte le direzioni ma solo di una casella.\n" +
                            "-Regina: \n" +
                            "\tSi muove in tutte le direzioni di tutte le caselle che vuole.");
        }

        private void nuovaPartitaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.Main();
        }

        private void abbandonaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult confirmResult = MessageBox.Show("Sei sicuro di voler abbandonare la partita?", "Conferma Uscita", MessageBoxButtons.YesNo);
            if (confirmResult == DialogResult.Yes)
            {
                Send("exit");
                Application.Exit();
            }
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            DialogResult confirmResult = MessageBox.Show("Sei sicuro di voler abbandonare la partita?", "Conferma Uscita", MessageBoxButtons.YesNo);
            if (confirmResult == DialogResult.Yes)
            {
                Send("exit");
                Application.Exit();
            }
        }
    }
}
