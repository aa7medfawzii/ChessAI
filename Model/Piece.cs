namespace ChessAI.Models
{
    // ── What TYPE of piece is it? ──────────────────────────────────────────
    public enum PieceType
    {
        None,       // empty square
        Pawn,
        Knight,
        Bishop,
        Rook,
        Queen,
        King
    }

    // ── Which SIDE does it belong to? ──────────────────────────────────────
    public enum PieceColor
    {
        None,       // used for empty squares
        White,
        Black
    }

    // ── A single chess piece: combines type + color ────────────────────────
    public class Piece
    {
        public PieceType  Type  { get; set; }
        public PieceColor Color { get; set; }

        // Convenience: is this square empty?
        public bool IsEmpty => Type == PieceType.None;

        // Constructor for a real piece
        public Piece(PieceType type, PieceColor color)
        {
            Type  = type;
            Color = color;
        }

        // Static shortcut for an empty square
        public static Piece Empty => new Piece(PieceType.None, PieceColor.None);

        // ── Unicode symbols for Console/debug printing ─────────────────────
        public override string ToString()
        {
            if (IsEmpty) return ".";

            return (Color, Type) switch
            {
                (PieceColor.White, PieceType.King)   => "♔",
                (PieceColor.White, PieceType.Queen)  => "♕",
                (PieceColor.White, PieceType.Rook)   => "♖",
                (PieceColor.White, PieceType.Bishop) => "♗",
                (PieceColor.White, PieceType.Knight) => "♘",
                (PieceColor.White, PieceType.Pawn)   => "♙",
                (PieceColor.Black, PieceType.King)   => "♚",
                (PieceColor.Black, PieceType.Queen)  => "♛",
                (PieceColor.Black, PieceType.Rook)   => "♜",
                (PieceColor.Black, PieceType.Bishop) => "♝",
                (PieceColor.Black, PieceType.Knight) => "♞",
                (PieceColor.Black, PieceType.Pawn)   => "♟",
                _                                    => "?"
            };
        }

        // Deep copy — important when the AI simulates moves
        public Piece Clone() => new Piece(Type, Color);
    }
}