using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChessAI.AI;
using ChessAI.Logic;
using ChessAI.Models;

namespace ChessAI.UI
{
    // ── Main application window ────────────────────────────────────────────
    // Wires together: BoardPanel (UI) ↔ GameState (data) ↔ MinimaxEngine (AI)
    // ────────────────────────────────────────────────────────────────────────
    public class ChessForm : Form
    {
        // ── Core components ────────────────────────────────────────────────
        private GameState     _state      = null!;
        private MinimaxEngine _engine     = null!;
        private BoardPanel    _boardPanel = null!;

        // ── Sidebar controls ───────────────────────────────────────────────
        private Label   _lblStatus   = null!;
        private Label   _lblThinking = null!;
        private Label   _lblNodes    = null!;
        private Label   _lblPruned   = null!;
        private ListBox _lstMoves    = null!;
        private Button  _btnNewGame  = null!;
        private Button  _btnUndo     = null!;
        private Button  _btnFlip     = null!;
        private Panel   _sidebar     = null!;

        // ── AI runs on a background thread to keep UI responsive ───────────
        private bool _aiThinking = false;

        public ChessForm()
        {
            InitializeComponents();
            StartNewGame();
        }

        // ────────────────────────────────────────────────────────────────────
        // FORM LAYOUT — built entirely in code (no Designer file needed)
        // ────────────────────────────────────────────────────────────────────
        private void InitializeComponents()
        {
            // ── Form settings ──────────────────────────────────────────────
            this.Text            = "♟ ChessAI — Minimax + Alpha-Beta Pruning";
            this.Size            = new Size(900, 680);
            this.MinimumSize     = new Size(700, 560);
            this.BackColor       = Color.FromArgb(32, 32, 32);
            this.Font            = new Font("Segoe UI", 9f);
            this.StartPosition   = FormStartPosition.CenterScreen;

            // ── Board panel (left side, square) ───────────────────────────
            _boardPanel = new BoardPanel
            {
                Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
                Location = new Point(10, 10),
            };
            _boardPanel.OnMoveMade += OnPlayerMove;

            // ── Sidebar (right side) ───────────────────────────────────────
            _sidebar = new Panel
            {
                BackColor = Color.FromArgb(45, 45, 45),
                Anchor    = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right,
            };

            // ── Status labels ──────────────────────────────────────────────
            _lblStatus   = MakeSidebarLabel("Your turn  (White ♙)", bold: true, fontSize: 11);
            _lblThinking = MakeSidebarLabel("AI: —");
            _lblNodes    = MakeSidebarLabel("Nodes evaluated: 0");
            _lblPruned   = MakeSidebarLabel("Branches pruned: 0");

            // ── Move history list ──────────────────────────────────────────
            var lblHistory = MakeSidebarLabel("Move History", bold: true);
            _lstMoves = new ListBox
            {
                BackColor    = Color.FromArgb(30, 30, 30),
                ForeColor    = Color.FromArgb(200, 200, 200),
                BorderStyle  = BorderStyle.None,
                Font         = new Font("Consolas", 9f),
                ScrollAlwaysVisible = false,
            };

            // ── Buttons ────────────────────────────────────────────────────
            _btnNewGame = MakeButton("New Game",  Color.FromArgb(76, 153, 0));
            _btnUndo    = MakeButton("Undo Move", Color.FromArgb(180, 100, 0));
            _btnFlip    = MakeButton("Flip Board",Color.FromArgb(60, 100, 160));

            _btnNewGame.Click += (s, e) => StartNewGame();
            _btnUndo.Click    += (s, e) => UndoMove();
            _btnFlip.Click    += (s, e) => _boardPanel.FlipBoard();

            // ── Add controls to sidebar ────────────────────────────────────
            _sidebar.Controls.Add(_lblStatus);
            _sidebar.Controls.Add(_lblThinking);
            _sidebar.Controls.Add(_lblNodes);
            _sidebar.Controls.Add(_lblPruned);
            _sidebar.Controls.Add(lblHistory);
            _sidebar.Controls.Add(_lstMoves);
            _sidebar.Controls.Add(_btnNewGame);
            _sidebar.Controls.Add(_btnUndo);
            _sidebar.Controls.Add(_btnFlip);

            // ── Add to form ────────────────────────────────────────────────
            this.Controls.Add(_boardPanel);
            this.Controls.Add(_sidebar);

            // ── Layout on resize ───────────────────────────────────────────
            this.Resize += OnFormResize;
            OnFormResize(null, null);
        }

        // Reflow layout every time the window is resized
        private void OnFormResize(object? sender, EventArgs? e)
        {
            int margin     = 10;
            int boardSize  = Math.Min(ClientSize.Height - 2 * margin,
                                      ClientSize.Width - 220 - 2 * margin);
            int sidebarX   = margin + boardSize + margin;
            int sidebarW   = ClientSize.Width - sidebarX - margin;

            _boardPanel.Location = new Point(margin, margin);
            _boardPanel.Size     = new Size(boardSize, boardSize);

            _sidebar.Location = new Point(sidebarX, margin);
            _sidebar.Size     = new Size(sidebarW, ClientSize.Height - 2 * margin);

            // Stack sidebar controls vertically
            int y  = 12;
            int pw = sidebarW - 16;

            void Place(Control c, int h) { c.SetBounds(8, y, pw, h); y += h + 6; }

            Place(_lblStatus,   28);
            Place(_lblThinking, 22);
            Place(_lblNodes,    22);
            Place(_lblPruned,   22);
            y += 8;
            Place(_sidebar.Controls[4], 20);   // lblHistory
            Place(_lstMoves, Math.Max(80, _sidebar.Height - y - 110));
            y += 10;
            Place(_btnNewGame, 34);
            Place(_btnUndo,    34);
            Place(_btnFlip,    34);
        }

        // ────────────────────────────────────────────────────────────────────
        // GAME FLOW
        // ────────────────────────────────────────────────────────────────────
        private void StartNewGame()
        {
            _state  = new GameState();
            _engine = new MinimaxEngine(depth: 3);

            _lstMoves.Items.Clear();
            UpdateStatus();

            _boardPanel.SetState(_state);
            _boardPanel.SetLastMove(null);
        }

        // ── Called by BoardPanel when the human makes a move ──────────────
        private async void OnPlayerMove(Move move)
        {
            if (_aiThinking) return;

            // Apply human move
            _state.ApplyMove(move);
            _boardPanel.SetLastMove(move);
            _boardPanel.SetState(_state);

            LogMove(move, isAI: false);
            UpdateStatus();

            // Check game over after human move
            if (CheckGameOver()) return;

            // ── Let AI respond asynchronously ─────────────────────────────
            _aiThinking      = true;
            _lblThinking.Text = "AI: thinking…";
            _btnUndo.Enabled  = false;

            Move? aiMove = await Task.Run(() => _engine.GetBestMove(_state));

            _aiThinking = false;

            if (aiMove == null) { CheckGameOver(); return; }

            _state.ApplyMove(aiMove);
            _boardPanel.SetLastMove(aiMove);
            _boardPanel.SetState(_state);

            // Update diagnostics
            _lblThinking.Text = "AI: done";
            _lblNodes.Text    = $"Nodes evaluated: {_engine.NodesEvaluated:N0}";
            _lblPruned.Text   = $"Branches pruned: {_engine.BranchesPruned:N0}";

            LogMove(aiMove, isAI: true);
            UpdateStatus();
            CheckGameOver();

            _btnUndo.Enabled = true;
        }

        // ── Undo: remove AI move + human move ─────────────────────────────
        private void UndoMove()
        {
            if (_aiThinking) return;

            // We need to undo two half-moves (AI + human)
            // Simplest approach: replay from scratch minus last 2 moves
            var history = new System.Collections.Generic.List<Move>(_state.MoveHistory);
            if (history.Count < 2) return;

            history.RemoveAt(history.Count - 1);  // remove AI move
            history.RemoveAt(history.Count - 1);  // remove player move

            _state = new GameState();
            foreach (var m in history) _state.ApplyMove(m);

            _lstMoves.Items.Clear();
            for (int i = 0; i < history.Count; i++)
                _lstMoves.Items.Add(FormatMove(history[i], i % 2 == 1));

            _boardPanel.SetLastMove(history.Count > 0 ? history[^1] : null);
            _boardPanel.SetState(_state);
            UpdateStatus();
        }

        // ────────────────────────────────────────────────────────────────────
        // HELPERS
        // ────────────────────────────────────────────────────────────────────

        private bool CheckGameOver()
        {
            if (MoveValidator.IsCheckmate(_state))
            {
                string winner = _state.CurrentTurn == PieceColor.White ? "Black" : "White";
                ShowGameOver($"Checkmate! {winner} wins! 🎉");
                return true;
            }
            if (MoveValidator.IsStalemate(_state))
            {
                ShowGameOver("Stalemate — it's a draw! 🤝");
                return true;
            }
            if (MoveValidator.IsFiftyMoveRule(_state))
            {
                ShowGameOver("Draw by 50-move rule.");
                return true;
            }
            return false;
        }

        private void ShowGameOver(string message)
        {
            MessageBox.Show(message, "Game Over",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateStatus()
        {
            bool isWhite = _state.CurrentTurn == PieceColor.White;
            bool inCheck = MoveValidator.IsKingInCheck(_state, _state.CurrentTurn);

            string turn   = isWhite ? "White ♙" : "Black ♟";
            string check  = inCheck ? "  ⚠ CHECK!" : "";
            string player = isWhite ? "Your turn" : "AI thinking…";

            _lblStatus.Text      = $"{player}  ({turn}){check}";
            _lblStatus.ForeColor = inCheck ? Color.Tomato : Color.FromArgb(180, 220, 130);
        }

        private void LogMove(Move move, bool isAI)
        {
            int    number = (_state.MoveHistory.Count);
            string entry  = FormatMove(move, isAI);
            _lstMoves.Items.Add(entry);
            _lstMoves.TopIndex = _lstMoves.Items.Count - 1;
        }

        private string FormatMove(Move move, bool isAI)
        {
            int    n    = _lstMoves.Items.Count + 1;
            string side = isAI ? "AI  " : "You ";
            return $"{n,3}. {side}  {move}";
        }

        // ── UI factory helpers ─────────────────────────────────────────────
        private Label MakeSidebarLabel(string text, bool bold = false, float fontSize = 9f)
        {
            return new Label
            {
                Text      = text,
                ForeColor = Color.FromArgb(200, 200, 200),
                Font      = new Font("Segoe UI", fontSize,
                                bold ? FontStyle.Bold : FontStyle.Regular),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft,
            };
        }

        private Button MakeButton(string text, Color backColor)
        {
            return new Button
            {
                Text      = text,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor    = Cursors.Hand,
            };
        }
    }
}