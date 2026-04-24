namespace ChessAI.Models
{
    // ── A position on the board: row 0-7, col 0-7 ─────────────────────────
    //    Row 0 = rank 8 (Black's back rank)
    //    Row 7 = rank 1 (White's back rank)
    //    Col 0 = file a,  Col 7 = file h
    public struct Square
    {
        public int Row { get; }
        public int Col { get; }

        public Square(int row, int col)
        {
            Row = row;
            Col = col;
        }

        // Is this position inside the 8×8 board?
        public bool IsValid => Row >= 0 && Row < 8 && Col >= 0 && Col < 8;

        // Human-readable: e.g.  (1, 4) → "e7"
        public override string ToString()
        {
            char file = (char)('a' + Col);   // col 0='a' … col 7='h'
            int  rank = 8 - Row;             // row 0=8  … row 7=1
            return $"{file}{rank}";
        }

        public bool Equals(Square other) => Row == other.Row && Col == other.Col;
    }

    // ── One chess move: from → to, plus optional metadata ─────────────────
    public class Move
    {
        public Square From { get; }
        public Square To   { get; }

        // Pawn promotion: what piece does the pawn become?
        // Defaults to Queen (best choice in most cases)
        public PieceType PromotionPiece { get; set; } = PieceType.Queen;

        // Flags so GameRules can handle special cases quickly
        public bool IsCastleKingside  { get; set; } = false;
        public bool IsCastleQueenside { get; set; } = false;
        public bool IsEnPassant       { get; set; } = false;

        public Move(Square from, Square to)
        {
            From = from;
            To   = to;
        }

        // e.g. "e2e4", "e7e8Q" (promotion)
        public override string ToString()
        {
            string promo = PromotionPiece != PieceType.Queen && PromotionPiece != PieceType.None
                ? PromotionPiece.ToString()[0].ToString()
                : "";
            return $"{From}{To}{promo}";
        }
    }
}