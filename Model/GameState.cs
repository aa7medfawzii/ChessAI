using System.Collections.Generic;

namespace ChessAI.Models
{
    // ── The entire state of a chess game at one moment in time ─────────────
    public class GameState
    {
        // 8×8 grid of pieces  [row, col]
        // Access: Board[0,0] = top-left = a8
        public Piece[,] Board { get; private set; } = new Piece[8, 8];

        // Whose turn is it?
        public PieceColor CurrentTurn { get; set; } = PieceColor.White;

        // ── Castling rights (lost when king or rook moves) ─────────────────
        public bool WhiteCanCastleKingside  { get; set; } = true;
        public bool WhiteCanCastleQueenside { get; set; } = true;
        public bool BlackCanCastleKingside  { get; set; } = true;
        public bool BlackCanCastleQueenside { get; set; } = true;

        // ── En passant target ──────────────────────────────────────────────
        // If a pawn just moved two squares, this is the square it passed through.
        // null means no en passant is available this turn.
        public Square? EnPassantTarget { get; set; } = null;

        // ── Move history (for undo & move log display) ─────────────────────
        public List<Move> MoveHistory { get; } = new List<Move>();

        // ── Half-move clock (for 50-move rule) ────────────────────────────
        public int HalfMoveClock { get; set; } = 0;

        // ────────────────────────────────────────────────────────────────────
        // CONSTRUCTOR — sets up the standard starting position
        // ────────────────────────────────────────────────────────────────────
        public GameState()
        {
            InitializeBoard();
        }

        private void InitializeBoard()
        {
            // Fill everything with empty first
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    Board[r, c] = Piece.Empty;

            // ── Black pieces (top, rows 0-1) ───────────────────────────────
            PlaceBackRank(0, PieceColor.Black);
            for (int c = 0; c < 8; c++)
                Board[1, c] = new Piece(PieceType.Pawn, PieceColor.Black);

            // ── White pieces (bottom, rows 6-7) ───────────────────────────
            for (int c = 0; c < 8; c++)
                Board[6, c] = new Piece(PieceType.Pawn, PieceColor.White);
            PlaceBackRank(7, PieceColor.White);
        }

        private void PlaceBackRank(int row, PieceColor color)
        {
            Board[row, 0] = new Piece(PieceType.Rook,   color);
            Board[row, 1] = new Piece(PieceType.Knight, color);
            Board[row, 2] = new Piece(PieceType.Bishop, color);
            Board[row, 3] = new Piece(PieceType.Queen,  color);
            Board[row, 4] = new Piece(PieceType.King,   color);
            Board[row, 5] = new Piece(PieceType.Bishop, color);
            Board[row, 6] = new Piece(PieceType.Knight, color);
            Board[row, 7] = new Piece(PieceType.Rook,   color);
        }

        // ────────────────────────────────────────────────────────────────────
        // GET / SET helpers
        // ────────────────────────────────────────────────────────────────────
        public Piece GetPiece(Square sq)          => Board[sq.Row, sq.Col];
        public Piece GetPiece(int row, int col)   => Board[row, col];
        public void  SetPiece(Square sq, Piece p) => Board[sq.Row, sq.Col] = p;

        public PieceColor Opponent =>
            CurrentTurn == PieceColor.White ? PieceColor.Black : PieceColor.White;

        // ────────────────────────────────────────────────────────────────────
        // APPLY MOVE — mutates the board (used by engine simulations too)
        // ────────────────────────────────────────────────────────────────────
        public void ApplyMove(Move move)
        {
            Piece moving = GetPiece(move.From);

            // ── En passant capture ────────────────────────────────────────
            if (move.IsEnPassant)
            {
                // Remove the captured pawn (it's on the same row as the FROM square)
                int capturedRow = move.From.Row;
                int capturedCol = move.To.Col;
                Board[capturedRow, capturedCol] = Piece.Empty;
            }

            // ── Castling: also move the rook ──────────────────────────────
            if (move.IsCastleKingside)
            {
                // Move rook from h-file to f-file
                Board[move.From.Row, 5] = Board[move.From.Row, 7];
                Board[move.From.Row, 7] = Piece.Empty;
            }
            if (move.IsCastleQueenside)
            {
                // Move rook from a-file to d-file
                Board[move.From.Row, 3] = Board[move.From.Row, 0];
                Board[move.From.Row, 0] = Piece.Empty;
            }

            // ── Promotion ─────────────────────────────────────────────────
            if (moving.Type == PieceType.Pawn &&
                (move.To.Row == 0 || move.To.Row == 7))
            {
                moving = new Piece(move.PromotionPiece, moving.Color);
            }

            // ── Normal move ───────────────────────────────────────────────
            SetPiece(move.To,   moving);
            SetPiece(move.From, Piece.Empty);

            // ── Update en passant target ──────────────────────────────────
            if (moving.Type == PieceType.Pawn &&
                System.Math.Abs(move.To.Row - move.From.Row) == 2)
            {
                // Pawn moved two squares → set en passant target to the skipped square
                int epRow = (move.From.Row + move.To.Row) / 2;
                EnPassantTarget = new Square(epRow, move.From.Col);
            }
            else
            {
                EnPassantTarget = null;
            }

            // ── Update castling rights ────────────────────────────────────
            UpdateCastlingRights(moving, move.From);

            // ── Half-move clock (reset on pawn move or capture) ───────────
            bool isCapture = !GetPiece(move.To).IsEmpty;  // checked before move
            if (moving.Type == PieceType.Pawn || isCapture)
                HalfMoveClock = 0;
            else
                HalfMoveClock++;

            // ── Flip turn ─────────────────────────────────────────────────
            MoveHistory.Add(move);
            CurrentTurn = Opponent;
        }

        private void UpdateCastlingRights(Piece moving, Square from)
        {
            if (moving.Type == PieceType.King)
            {
                if (moving.Color == PieceColor.White)
                { WhiteCanCastleKingside = false; WhiteCanCastleQueenside = false; }
                else
                { BlackCanCastleKingside = false; BlackCanCastleQueenside = false; }
            }
            if (moving.Type == PieceType.Rook)
            {
                if (from.Row == 7 && from.Col == 0) WhiteCanCastleQueenside = false;
                if (from.Row == 7 && from.Col == 7) WhiteCanCastleKingside  = false;
                if (from.Row == 0 && from.Col == 0) BlackCanCastleQueenside = false;
                if (from.Row == 0 && from.Col == 7) BlackCanCastleKingside  = false;
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // CLONE — the AI calls this to simulate moves without touching the
        // real board
        // ────────────────────────────────────────────────────────────────────
        public GameState Clone()
        {
            var clone = new GameState();

            // Copy board pieces
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    clone.Board[r, c] = Board[r, c].Clone();

            clone.CurrentTurn            = CurrentTurn;
            clone.WhiteCanCastleKingside  = WhiteCanCastleKingside;
            clone.WhiteCanCastleQueenside = WhiteCanCastleQueenside;
            clone.BlackCanCastleKingside  = BlackCanCastleKingside;
            clone.BlackCanCastleQueenside = BlackCanCastleQueenside;
            clone.EnPassantTarget         = EnPassantTarget;
            clone.HalfMoveClock           = HalfMoveClock;

            return clone;
        }
    }
}