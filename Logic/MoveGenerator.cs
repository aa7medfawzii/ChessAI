using System.Collections.Generic;
using ChessAI.Models;

namespace ChessAI.Logic
{
    // ── Generates all PSEUDO-LEGAL moves for a given color ─────────────────
    // "Pseudo-legal" means the piece can physically make the move,
    // but we haven't checked yet if it leaves the king in check.
    // MoveValidator.cs will filter those out.
    public static class MoveGenerator
    {
        // ────────────────────────────────────────────────────────────────────
        // ENTRY POINT — get all pseudo-legal moves for one side
        // ────────────────────────────────────────────────────────────────────
        public static List<Move> GetAllMoves(GameState state, PieceColor color)
        {
            var moves = new List<Move>();

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    Piece piece = state.GetPiece(row, col);
                    if (piece.IsEmpty || piece.Color != color) continue;

                    var from = new Square(row, col);
                    moves.AddRange(GetMovesForPiece(state, from));
                }
            }

            return moves;
        }

        // ────────────────────────────────────────────────────────────────────
        // DISPATCHER — route to the correct piece generator
        // ────────────────────────────────────────────────────────────────────
        public static List<Move> GetMovesForPiece(GameState state, Square from)
        {
            Piece piece = state.GetPiece(from);

            return piece.Type switch
            {
                PieceType.Pawn   => GetPawnMoves(state, from),
                PieceType.Knight => GetKnightMoves(state, from),
                PieceType.Bishop => GetSlidingMoves(state, from, bishopDirections),
                PieceType.Rook   => GetSlidingMoves(state, from, rookDirections),
                PieceType.Queen  => GetSlidingMoves(state, from, queenDirections),
                PieceType.King   => GetKingMoves(state, from),
                _                => new List<Move>()
            };
        }

        // ────────────────────────────────────────────────────────────────────
        // DIRECTION TABLES
        // Each entry is (deltaRow, deltaCol)
        // ────────────────────────────────────────────────────────────────────
        private static readonly (int, int)[] bishopDirections =
            { (-1,-1), (-1,+1), (+1,-1), (+1,+1) };

        private static readonly (int, int)[] rookDirections =
            { (-1, 0), (+1, 0), (0,-1), (0,+1) };

        private static readonly (int, int)[] queenDirections =
            { (-1,-1), (-1,+1), (+1,-1), (+1,+1),
              (-1, 0), (+1, 0), (0,-1), (0,+1) };

        private static readonly (int, int)[] knightJumps =
            { (-2,-1), (-2,+1), (-1,-2), (-1,+2),
              (+1,-2), (+1,+2), (+2,-1), (+2,+1) };

        private static readonly (int, int)[] kingSteps =
            { (-1,-1), (-1, 0), (-1,+1),
              ( 0,-1),          ( 0,+1),
              (+1,-1), (+1, 0), (+1,+1) };

        // ────────────────────────────────────────────────────────────────────
        // PAWN MOVES
        // Pawns are the most complex piece: direction depends on color,
        // captures are diagonal, and they have two special rules.
        // ────────────────────────────────────────────────────────────────────
        private static List<Move> GetPawnMoves(GameState state, Square from)
        {
            var moves = new List<Move>();
            Piece pawn  = state.GetPiece(from);

            // White moves UP the board (decreasing row index)
            // Black moves DOWN (increasing row index)
            int direction  = pawn.Color == PieceColor.White ? -1 : +1;
            int startRow   = pawn.Color == PieceColor.White ?  6 :  1;
            int promoteRow = pawn.Color == PieceColor.White ?  0 :  7;

            // ── 1. Single step forward ─────────────────────────────────────
            int oneStep = from.Row + direction;
            if (IsInBounds(oneStep, from.Col))
            {
                var to = new Square(oneStep, from.Col);
                if (state.GetPiece(to).IsEmpty)
                {
                    AddPawnMove(moves, from, to, promoteRow);

                    // ── 2. Double step from starting row ───────────────────
                    int twoStep = from.Row + 2 * direction;
                    if (from.Row == startRow && IsInBounds(twoStep, from.Col))
                    {
                        var to2 = new Square(twoStep, from.Col);
                        if (state.GetPiece(to2).IsEmpty)
                            moves.Add(new Move(from, to2));
                    }
                }
            }

            // ── 3. Diagonal captures ───────────────────────────────────────
            foreach (int dc in new[] { -1, +1 })
            {
                int captureRow = from.Row + direction;
                int captureCol = from.Col + dc;
                if (!IsInBounds(captureRow, captureCol)) continue;

                var to = new Square(captureRow, captureCol);
                Piece target = state.GetPiece(to);

                // Normal diagonal capture
                if (!target.IsEmpty && target.Color != pawn.Color)
                    AddPawnMove(moves, from, to, promoteRow);

                // ── 4. En passant ──────────────────────────────────────────
                if (state.EnPassantTarget.HasValue &&
                    state.EnPassantTarget.Value.Equals(to))
                {
                    var epMove = new Move(from, to) { IsEnPassant = true };
                    moves.Add(epMove);
                }
            }

            return moves;
        }

        // Helper: if the pawn reaches the promotion row, generate 4 promotion
        // moves (Queen, Rook, Bishop, Knight). Otherwise just add one move.
        private static void AddPawnMove(List<Move> moves, Square from, Square to, int promoteRow)
        {
            if (to.Row == promoteRow)
            {
                foreach (var promo in new[]
                {
                    PieceType.Queen, PieceType.Rook,
                    PieceType.Bishop, PieceType.Knight
                })
                {
                    moves.Add(new Move(from, to) { PromotionPiece = promo });
                }
            }
            else
            {
                moves.Add(new Move(from, to));
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // KNIGHT MOVES — L-shaped jumps, can leap over pieces
        // ────────────────────────────────────────────────────────────────────
        private static List<Move> GetKnightMoves(GameState state, Square from)
        {
            var moves = new List<Move>();
            Piece knight = state.GetPiece(from);

            foreach (var (dr, dc) in knightJumps)
            {
                int r = from.Row + dr;
                int c = from.Col + dc;
                if (!IsInBounds(r, c)) continue;

                Piece target = state.GetPiece(r, c);

                // Can land on empty squares or enemy pieces, NOT friendly pieces
                if (target.IsEmpty || target.Color != knight.Color)
                    moves.Add(new Move(from, new Square(r, c)));
            }

            return moves;
        }

        // ────────────────────────────────────────────────────────────────────
        // SLIDING MOVES — Bishop, Rook, Queen
        // They slide in a direction until they hit a piece or the board edge.
        // ────────────────────────────────────────────────────────────────────
        private static List<Move> GetSlidingMoves(
            GameState state, Square from, (int, int)[] directions)
        {
            var moves = new List<Move>();
            Piece slider = state.GetPiece(from);

            foreach (var (dr, dc) in directions)
            {
                int r = from.Row + dr;
                int c = from.Col + dc;

                // Keep sliding until we fall off the board
                while (IsInBounds(r, c))
                {
                    Piece target = state.GetPiece(r, c);

                    if (target.IsEmpty)
                    {
                        // Empty square — add and keep sliding
                        moves.Add(new Move(from, new Square(r, c)));
                    }
                    else if (target.Color != slider.Color)
                    {
                        // Enemy piece — capture it, then STOP (can't jump over)
                        moves.Add(new Move(from, new Square(r, c)));
                        break;
                    }
                    else
                    {
                        // Friendly piece — blocked, stop immediately
                        break;
                    }

                    r += dr;
                    c += dc;
                }
            }

            return moves;
        }

        // ────────────────────────────────────────────────────────────────────
        // KING MOVES — one step in any direction + castling
        // ────────────────────────────────────────────────────────────────────
        private static List<Move> GetKingMoves(GameState state, Square from)
        {
            var moves = new List<Move>();
            Piece king = state.GetPiece(from);

            // ── Normal one-step moves ──────────────────────────────────────
            foreach (var (dr, dc) in kingSteps)
            {
                int r = from.Row + dr;
                int c = from.Col + dc;
                if (!IsInBounds(r, c)) continue;

                Piece target = state.GetPiece(r, c);
                if (target.IsEmpty || target.Color != king.Color)
                    moves.Add(new Move(from, new Square(r, c)));
            }

            // ── Castling ───────────────────────────────────────────────────
            // We only add the move here; MoveValidator will verify the king
            // is not in check and does not pass through an attacked square.

            if (king.Color == PieceColor.White && from.Row == 7 && from.Col == 4)
            {
                // Kingside (O-O): squares f1 and g1 must be empty
                if (state.WhiteCanCastleKingside &&
                    state.GetPiece(7, 5).IsEmpty &&
                    state.GetPiece(7, 6).IsEmpty)
                {
                    moves.Add(new Move(from, new Square(7, 6))
                        { IsCastleKingside = true });
                }

                // Queenside (O-O-O): squares b1, c1, d1 must be empty
                if (state.WhiteCanCastleQueenside &&
                    state.GetPiece(7, 3).IsEmpty &&
                    state.GetPiece(7, 2).IsEmpty &&
                    state.GetPiece(7, 1).IsEmpty)
                {
                    moves.Add(new Move(from, new Square(7, 2))
                        { IsCastleQueenside = true });
                }
            }

            if (king.Color == PieceColor.Black && from.Row == 0 && from.Col == 4)
            {
                // Kingside
                if (state.BlackCanCastleKingside &&
                    state.GetPiece(0, 5).IsEmpty &&
                    state.GetPiece(0, 6).IsEmpty)
                {
                    moves.Add(new Move(from, new Square(0, 6))
                        { IsCastleKingside = true });
                }

                // Queenside
                if (state.BlackCanCastleQueenside &&
                    state.GetPiece(0, 3).IsEmpty &&
                    state.GetPiece(0, 2).IsEmpty &&
                    state.GetPiece(0, 1).IsEmpty)
                {
                    moves.Add(new Move(from, new Square(0, 2))
                        { IsCastleQueenside = true });
                }
            }

            return moves;
        }

        // ────────────────────────────────────────────────────────────────────
        // UTILITY
        // ────────────────────────────────────────────────────────────────────
        public static bool IsInBounds(int row, int col) =>
            row >= 0 && row < 8 && col >= 0 && col < 8;

        // Find the king's square for a given color
        public static Square FindKing(GameState state, PieceColor color)
        {
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                {
                    Piece p = state.GetPiece(r, c);
                    if (p.Type == PieceType.King && p.Color == color)
                        return new Square(r, c);
                }

            // Should never happen in a valid game
            throw new System.Exception($"King not found for {color}");
        }
    }
}