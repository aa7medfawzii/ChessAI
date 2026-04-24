using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ChessAI.Models;
using ChessAI.Logic;

namespace ChessAI.UI
{
    // ── Custom Panel: renders the board and handles player interaction ──────
    //
    // Responsibilities:
    //   - Draw 64 squares with correct colors
    //   - Draw pieces as Unicode text (no image files needed)
    //   - Highlight: selected square, legal move dots, last move, check
    //   - Handle click-to-move (click piece → click destination)
    // ────────────────────────────────────────────────────────────────────────
    public class BoardPanel : Panel
    {
        // ── Colors ─────────────────────────────────────────────────────────
        private readonly Color LightSquare      = Color.FromArgb(240, 217, 181);
        private readonly Color DarkSquare       = Color.FromArgb(181, 136,  99);
        private readonly Color SelectedColor    = Color.FromArgb(130, 151,  105);
        private readonly Color LegalMoveColor   = Color.FromArgb(100, 111,  64);
        private readonly Color LastMoveColor    = Color.FromArgb(205, 210,  106);
        private readonly Color CheckColor       = Color.FromArgb(220,  50,  50);

        // ── State ──────────────────────────────────────────────────────────
        private GameState       _state;
        private Square?         _selectedSquare;
        private List<Move>      _legalMovesForSelected = new List<Move>();
        private Move            _lastMove;
        private bool            _flipped = false;   // flip board for Black's perspective

        // ── Events (ChessForm subscribes to these) ─────────────────────────
        public event Action<Move> OnMoveMade;

        // ── Font for piece rendering ───────────────────────────────────────
        private Font _pieceFont;

        public BoardPanel()
        {
            DoubleBuffered = true;   // prevents flickering on redraw
            ResizeRedraw  = true;

            this.Click    += BoardPanel_Click;
            this.Resize   += (s, e) => UpdatePieceFont();
        }

        // ────────────────────────────────────────────────────────────────────
        // PUBLIC API — called by ChessForm
        // ────────────────────────────────────────────────────────────────────
        public void SetState(GameState state)
        {
            _state          = state;
            _selectedSquare = null;
            _legalMovesForSelected.Clear();
            Invalidate();  // triggers OnPaint
        }

        public void SetLastMove(Move move)
        {
            _lastMove = move;
        }

        public void ClearSelection()
        {
            _selectedSquare = null;
            _legalMovesForSelected.Clear();
            Invalidate();
        }

        public void FlipBoard()
        {
            _flipped = !_flipped;
            Invalidate();
        }

        // ────────────────────────────────────────────────────────────────────
        // DRAWING — called automatically by Windows whenever the panel needs
        // to be redrawn (on SetState, resize, etc.)
        // ────────────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            if (_state == null) return;

            Graphics g         = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            int squareSize = GetSquareSize();

            for (int row = 0; row < 8; row++)
            for (int col = 0; col < 8; col++)
            {
                // If board is flipped, mirror both axes
                int drawRow = _flipped ? 7 - row : row;
                int drawCol = _flipped ? 7 - col : col;

                int x = drawCol * squareSize;
                int y = drawRow * squareSize;

                var sq    = new Square(row, col);
                var rect  = new Rectangle(x, y, squareSize, squareSize);

                // ── 1. Square background color ─────────────────────────────
                Color bg = ((row + col) % 2 == 0) ? LightSquare : DarkSquare;

                // Last move highlight
                if (_lastMove != null &&
                    (_lastMove.From.Equals(sq) || _lastMove.To.Equals(sq)))
                    bg = LastMoveColor;

                // Selected square highlight
                if (_selectedSquare.HasValue && _selectedSquare.Value.Equals(sq))
                    bg = SelectedColor;

                // Check highlight: king's square glows red
                if (_state != null)
                {
                    Piece p = _state.GetPiece(sq);
                    if (p.Type == PieceType.King && p.Color == _state.CurrentTurn)
                    {
                        if (MoveValidator.IsKingInCheck(_state, p.Color))
                            bg = CheckColor;
                    }
                }

                g.FillRectangle(new SolidBrush(bg), rect);

                // ── 2. Legal move indicator ────────────────────────────────
                if (IsLegalTarget(sq))
                {
                    Piece target = _state.GetPiece(sq);
                    if (target.IsEmpty)
                    {
                        // Draw a small dot in the centre
                        int dot  = squareSize / 3;
                        int off  = (squareSize - dot) / 2;
                        g.FillEllipse(
                            new SolidBrush(Color.FromArgb(120, LegalMoveColor)),
                            x + off, y + off, dot, dot);
                    }
                    else
                    {
                        // Highlight enemy piece with a ring
                        using var pen = new Pen(Color.FromArgb(160, LegalMoveColor), 4);
                        g.DrawEllipse(pen, rect);
                    }
                }

                // ── 3. Piece ───────────────────────────────────────────────
                Piece piece = _state.GetPiece(sq);
                if (!piece.IsEmpty)
                    DrawPiece(g, piece, rect);
            }

            // ── 4. Board border ────────────────────────────────────────────
            g.DrawRectangle(Pens.Black, 0, 0, 8 * squareSize - 1, 8 * squareSize - 1);
        }

        // Draw a piece as a Unicode chess symbol, centred in its square
        private void DrawPiece(Graphics g, Piece piece, Rectangle rect)
        {
            if (_pieceFont == null) UpdatePieceFont();

            string symbol = piece.ToString();

            // Measure text to centre it
            SizeF sz     = g.MeasureString(symbol, _pieceFont);
            float tx     = rect.X + (rect.Width  - sz.Width)  / 2;
            float ty     = rect.Y + (rect.Height - sz.Height) / 2;

            // Shadow for visibility on any square color
            Color shadowColor = piece.Color == PieceColor.White
                ? Color.FromArgb(80, 0, 0, 0)
                : Color.FromArgb(80, 255, 255, 255);

            g.DrawString(symbol, _pieceFont, new SolidBrush(shadowColor), tx + 1.5f, ty + 1.5f);

            Color pieceColor = piece.Color == PieceColor.White
                ? Color.WhiteSmoke
                : Color.FromArgb(30, 20, 10);

            g.DrawString(symbol, _pieceFont, new SolidBrush(pieceColor), tx, ty);
        }

        // ────────────────────────────────────────────────────────────────────
        // CLICK HANDLING — click-to-move: first click selects, second moves
        // ────────────────────────────────────────────────────────────────────
        private void BoardPanel_Click(object sender, EventArgs e)
        {
            if (_state == null) return;

            var mouseEvent = (MouseEventArgs)e;
            Square clicked = PixelToSquare(mouseEvent.X, mouseEvent.Y);

            if (!clicked.IsValid) return;

            if (!_selectedSquare.HasValue)
            {
                // ── First click: try to select a piece ────────────────────
                Piece piece = _state.GetPiece(clicked);
                if (!piece.IsEmpty && piece.Color == _state.CurrentTurn)
                {
                    _selectedSquare = clicked;
                    _legalMovesForSelected = MoveValidator.GetLegalMovesForPiece(_state, clicked);
                    Invalidate();
                }
            }
            else
            {
                // ── Second click: try to make a move ──────────────────────
                Move move = FindMove(_selectedSquare.Value, clicked);

                if (move != null)
                {
                    // Valid move — fire event so ChessForm can process it
                    _selectedSquare = null;
                    _legalMovesForSelected.Clear();
                    OnMoveMade?.Invoke(move);
                }
                else
                {
                    // Clicked a different friendly piece → re-select
                    Piece piece = _state.GetPiece(clicked);
                    if (!piece.IsEmpty && piece.Color == _state.CurrentTurn)
                    {
                        _selectedSquare = clicked;
                        _legalMovesForSelected = MoveValidator.GetLegalMovesForPiece(_state, clicked);
                    }
                    else
                    {
                        // Clicked empty/invalid → deselect
                        _selectedSquare = null;
                        _legalMovesForSelected.Clear();
                    }

                    Invalidate();
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // HELPERS
        // ────────────────────────────────────────────────────────────────────

        // Convert pixel coordinates to board square
        private Square PixelToSquare(int x, int y)
        {
            int size = GetSquareSize();
            int col  = x / size;
            int row  = y / size;

            if (_flipped) { row = 7 - row; col = 7 - col; }

            return new Square(row, col);
        }

        // Find a legal move from 'from' to 'to' in the legal moves list
        private Move FindMove(Square from, Square to)
        {
            foreach (Move m in _legalMovesForSelected)
                if (m.From.Equals(from) && m.To.Equals(to))
                    return m;   // promotion: always returns Queen by default
            return null;
        }

        // Is this square a legal target for the selected piece?
        private bool IsLegalTarget(Square sq)
        {
            foreach (Move m in _legalMovesForSelected)
                if (m.To.Equals(sq)) return true;
            return false;
        }

        // Keep piece font proportional to square size
        private int GetSquareSize() => Math.Min(Width, Height) / 8;

        private void UpdatePieceFont()
        {
            int size = GetSquareSize();
            _pieceFont?.Dispose();
            _pieceFont = new Font("Segoe UI Symbol", size * 0.62f, FontStyle.Regular, GraphicsUnit.Pixel);
        }
    }
}