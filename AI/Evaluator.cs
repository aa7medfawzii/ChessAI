using ChessAI.Models;

namespace ChessAI.AI
{
    // ── Heuristic Evaluation Function ──────────────────────────────────────
    //
    // Scores a board position from White's perspective:
    //   Positive score → White is winning
    //   Negative score → Black is winning
    //   Zero           → roughly equal
    //
    // Three factors:
    //   1. Material Value    — how many/which pieces each side has
    //   2. Positional Value  — where the pieces are (piece-square tables)
    //   3. King Safety       — pawn shield + penalties for exposed king
    // ────────────────────────────────────────────────────────────────────────
    public static class Evaluator
    {
        // ────────────────────────────────────────────────────────────────────
        // 1. MATERIAL VALUES (centipawns — 1 pawn = 100)
        // ────────────────────────────────────────────────────────────────────
        public static int GetPieceValue(PieceType type) => type switch
        {
            PieceType.Pawn   => 100,
            PieceType.Knight => 320,
            PieceType.Bishop => 330,
            PieceType.Rook   => 500,
            PieceType.Queen  => 900,
            PieceType.King   => 20000,  // effectively infinite
            _                => 0
        };

        // ────────────────────────────────────────────────────────────────────
        // 2. PIECE-SQUARE TABLES
        //
        // Each table is 8×8 and represents bonus/penalty points for a WHITE
        // piece standing on that square. For Black we mirror the table
        // vertically (row 7-r).
        //
        // Values are in centipawns. Positive = good square, Negative = bad.
        // ────────────────────────────────────────────────────────────────────

        // Pawns: reward advancing, penalise edge files
        private static readonly int[,] PawnTable =
        {
            {  0,  0,  0,  0,  0,  0,  0,  0 },
            { 50, 50, 50, 50, 50, 50, 50, 50 },
            { 10, 10, 20, 30, 30, 20, 10, 10 },
            {  5,  5, 10, 25, 25, 10,  5,  5 },
            {  0,  0,  0, 20, 20,  0,  0,  0 },
            {  5, -5,-10,  0,  0,-10, -5,  5 },
            {  5, 10, 10,-20,-20, 10, 10,  5 },
            {  0,  0,  0,  0,  0,  0,  0,  0 }
        };

        // Knights: reward centre, penalise rim ("a knight on the rim is dim")
        private static readonly int[,] KnightTable =
        {
            { -50,-40,-30,-30,-30,-30,-40,-50 },
            { -40,-20,  0,  0,  0,  0,-20,-40 },
            { -30,  0, 10, 15, 15, 10,  0,-30 },
            { -30,  5, 15, 20, 20, 15,  5,-30 },
            { -30,  0, 15, 20, 20, 15,  0,-30 },
            { -30,  5, 10, 15, 15, 10,  5,-30 },
            { -40,-20,  0,  5,  5,  0,-20,-40 },
            { -50,-40,-30,-30,-30,-30,-40,-50 }
        };

        // Bishops: reward long diagonals, penalise corners
        private static readonly int[,] BishopTable =
        {
            { -20,-10,-10,-10,-10,-10,-10,-20 },
            { -10,  0,  0,  0,  0,  0,  0,-10 },
            { -10,  0,  5, 10, 10,  5,  0,-10 },
            { -10,  5,  5, 10, 10,  5,  5,-10 },
            { -10,  0, 10, 10, 10, 10,  0,-10 },
            { -10, 10, 10, 10, 10, 10, 10,-10 },
            { -10,  5,  0,  0,  0,  0,  5,-10 },
            { -20,-10,-10,-10,-10,-10,-10,-20 }
        };

        // Rooks: reward 7th rank and open files
        private static readonly int[,] RookTable =
        {
            {  0,  0,  0,  0,  0,  0,  0,  0 },
            {  5, 10, 10, 10, 10, 10, 10,  5 },
            { -5,  0,  0,  0,  0,  0,  0, -5 },
            { -5,  0,  0,  0,  0,  0,  0, -5 },
            { -5,  0,  0,  0,  0,  0,  0, -5 },
            { -5,  0,  0,  0,  0,  0,  0, -5 },
            { -5,  0,  0,  0,  0,  0,  0, -5 },
            {  0,  0,  0,  5,  5,  0,  0,  0 }
        };

        // Queen: slightly reward centre, avoid early development to rim
        private static readonly int[,] QueenTable =
        {
            { -20,-10,-10, -5, -5,-10,-10,-20 },
            { -10,  0,  0,  0,  0,  0,  0,-10 },
            { -10,  0,  5,  5,  5,  5,  0,-10 },
            {  -5,  0,  5,  5,  5,  5,  0, -5 },
            {   0,  0,  5,  5,  5,  5,  0, -5 },
            { -10,  5,  5,  5,  5,  5,  0,-10 },
            { -10,  0,  5,  0,  0,  0,  0,-10 },
            { -20,-10,-10, -5, -5,-10,-10,-20 }
        };

        // King middlegame: hide behind pawns, penalise centre exposure
        private static readonly int[,] KingMiddleTable =
        {
            { -30,-40,-40,-50,-50,-40,-40,-30 },
            { -30,-40,-40,-50,-50,-40,-40,-30 },
            { -30,-40,-40,-50,-50,-40,-40,-30 },
            { -30,-40,-40,-50,-50,-40,-40,-30 },
            { -20,-30,-30,-40,-40,-30,-30,-20 },
            { -10,-20,-20,-20,-20,-20,-20,-10 },
            {  20, 20,  0,  0,  0,  0, 20, 20 },
            {  20, 30, 10,  0,  0, 10, 30, 20 }
        };

        // King endgame: king should centralise and be active
        private static readonly int[,] KingEndTable =
        {
            { -50,-40,-30,-20,-20,-30,-40,-50 },
            { -30,-20,-10,  0,  0,-10,-20,-30 },
            { -30,-10, 20, 30, 30, 20,-10,-30 },
            { -30,-10, 30, 40, 40, 30,-10,-30 },
            { -30,-10, 30, 40, 40, 30,-10,-30 },
            { -30,-10, 20, 30, 30, 20,-10,-30 },
            { -30,-30,  0,  0,  0,  0,-30,-30 },
            { -50,-30,-30,-30,-30,-30,-30,-50 }
        };

        // ────────────────────────────────────────────────────────────────────
        // PIECE-SQUARE LOOKUP
        // White uses the table as-is (row 0 = rank 8 = their far side).
        // Black mirrors vertically so both sides use the same table logic.
        // ────────────────────────────────────────────────────────────────────
        private static int GetPositionBonus(Piece piece, int row, int col, bool isEndgame)
        {
            int tableRow = piece.Color == PieceColor.White ? row : 7 - row;

            return piece.Type switch
            {
                PieceType.Pawn   => PawnTable[tableRow, col],
                PieceType.Knight => KnightTable[tableRow, col],
                PieceType.Bishop => BishopTable[tableRow, col],
                PieceType.Rook   => RookTable[tableRow, col],
                PieceType.Queen  => QueenTable[tableRow, col],
                PieceType.King   => isEndgame
                                    ? KingEndTable[tableRow, col]
                                    : KingMiddleTable[tableRow, col],
                _ => 0
            };
        }

        // ────────────────────────────────────────────────────────────────────
        // 3. KING SAFETY
        //
        // Count pawn shield squares in front of the king.
        // Each missing pawn = -10 penalty.
        // ────────────────────────────────────────────────────────────────────
        private static int EvaluateKingSafety(GameState state, PieceColor color)
        {
            int score = 0;
            Square king = Logic.MoveGenerator.FindKing(state, color);

            // Direction the pawns stand relative to the king
            // White pawns shield from above (lower row index)
            // Black pawns shield from below (higher row index)
            int shieldDir = color == PieceColor.White ? -1 : +1;
            int shieldRow = king.Row + shieldDir;

            if (shieldRow < 0 || shieldRow > 7) return 0;

            // Check three squares directly in front of the king
            for (int dc = -1; dc <= 1; dc++)
            {
                int sc = king.Col + dc;
                if (sc < 0 || sc > 7) continue;

                Piece p = state.GetPiece(shieldRow, sc);
                if (p.Type != PieceType.Pawn || p.Color != color)
                    score -= 10;   // missing shield pawn
            }

            return score;
        }

        // ────────────────────────────────────────────────────────────────────
        // ENDGAME DETECTION
        // We're in the endgame if both sides have no queens,
        // or each side's material (excl. king/pawns) is low.
        // ────────────────────────────────────────────────────────────────────
        private static bool IsEndgame(GameState state)
        {
            int whiteMaterial = 0, blackMaterial = 0;

            for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                Piece p = state.GetPiece(r, c);
                if (p.IsEmpty || p.Type == PieceType.King || p.Type == PieceType.Pawn)
                    continue;

                if (p.Color == PieceColor.White) whiteMaterial += GetPieceValue(p.Type);
                else                              blackMaterial += GetPieceValue(p.Type);
            }

            // Endgame if both sides have less than a rook + minor piece worth of material
            return whiteMaterial <= 830 && blackMaterial <= 830;
        }

        // ────────────────────────────────────────────────────────────────────
        // MAIN EVALUATE — called by Minimax on leaf nodes
        // Returns score in centipawns from White's perspective
        // ────────────────────────────────────────────────────────────────────
        public static int Evaluate(GameState state)
        {
            bool endgame = IsEndgame(state);
            int  score   = 0;

            for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                Piece p = state.GetPiece(r, c);
                if (p.IsEmpty) continue;

                int pieceScore = GetPieceValue(p.Type)
                               + GetPositionBonus(p, r, c, endgame);

                // White adds, Black subtracts (score is from White's POV)
                if (p.Color == PieceColor.White) score += pieceScore;
                else                              score -= pieceScore;
            }

            // King safety bonus/penalty
            score += EvaluateKingSafety(state, PieceColor.White);
            score -= EvaluateKingSafety(state, PieceColor.Black);

            return score;
        }
    }
}