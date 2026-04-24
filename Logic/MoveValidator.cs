using System.Collections.Generic;
using ChessAI.Models;

namespace ChessAI.Logic
{
    // ── Filters pseudo-legal moves down to fully LEGAL moves ───────────────
    //
    // A move is illegal if it leaves (or puts) your own king in check.
    // Strategy: simulate the move on a cloned board, then check if the
    // king is attacked on that cloned board.
    //
    // Also handles the castling safety rules:
    //   - King must not be in check before castling
    //   - King must not pass through an attacked square
    //   - King must not land on an attacked square
    // ────────────────────────────────────────────────────────────────────────
    public static class MoveValidator
    {
        // ── Entry point: take pseudo-legal moves, return only legal ones ───
        public static List<Move> FilterLegalMoves(GameState state, List<Move> pseudoLegal)
        {
            var legal = new List<Move>();

            foreach (Move move in pseudoLegal)
            {
                if (IsLegal(state, move))
                    legal.Add(move);
            }

            return legal;
        }

        // ── Get fully legal moves for a piece on a given square ────────────
        public static List<Move> GetLegalMovesForPiece(GameState state, Square from)
        {
            var pseudo = MoveGenerator.GetMovesForPiece(state, from);
            return FilterLegalMoves(state, pseudo);
        }

        // ── Get all legal moves for the current player ────────────────────
        public static List<Move> GetAllLegalMoves(GameState state)
        {
            var pseudo = MoveGenerator.GetAllMoves(state, state.CurrentTurn);
            return FilterLegalMoves(state, pseudo);
        }

        // ────────────────────────────────────────────────────────────────────
        // CORE CHECK: is a specific move legal?
        // ────────────────────────────────────────────────────────────────────
        private static bool IsLegal(GameState state, Move move)
        {
            Piece moving = state.GetPiece(move.From);

            // ── Special case: castling has extra safety requirements ────────
            if (move.IsCastleKingside || move.IsCastleQueenside)
                return IsCastlingLegal(state, move);

            // ── General case: simulate → check if own king is safe ─────────
            GameState simulated = state.Clone();
            simulated.ApplyMove(move);

            // After the move it's the opponent's turn, so we check if the
            // player who just moved (moving.Color) left their king in check
            return !IsKingInCheck(simulated, moving.Color);
        }

        // ────────────────────────────────────────────────────────────────────
        // CASTLING LEGALITY
        // Three conditions beyond piece positions (already checked in generator):
        //   1. King must NOT currently be in check
        //   2. King must NOT pass through an attacked square
        //   3. King must NOT land on an attacked square
        // ────────────────────────────────────────────────────────────────────
        private static bool IsCastlingLegal(GameState state, Move move)
        {
            PieceColor color = state.GetPiece(move.From).Color;
            int row = move.From.Row;   // 7 for White, 0 for Black

            // Condition 1: king must not currently be in check
            if (IsKingInCheck(state, color)) return false;

            // Determine the intermediate square the king passes through
            int passThroughCol = move.IsCastleKingside ? 5 : 3;
            var passThrough    = new Square(row, passThroughCol);

            // Condition 2: king must not pass through an attacked square
            if (IsSquareAttackedBy(state, passThrough, Opponent(color))) return false;

            // Condition 3: simulate full move and check landing square
            GameState simulated = state.Clone();
            simulated.ApplyMove(move);
            return !IsKingInCheck(simulated, color);
        }

        // ────────────────────────────────────────────────────────────────────
        // IS THE KING OF 'color' IN CHECK on the given board state?
        // ────────────────────────────────────────────────────────────────────
        public static bool IsKingInCheck(GameState state, PieceColor color)
        {
            Square kingSquare = MoveGenerator.FindKing(state, color);
            return IsSquareAttackedBy(state, kingSquare, Opponent(color));
        }

        // ────────────────────────────────────────────────────────────────────
        // IS A SQUARE ATTACKED BY A GIVEN COLOR?
        //
        // We use a "reverse lookup" trick:
        //   Instead of generating all attacker moves, we ask:
        //   "If I placed a [piece] on this square, could it attack an enemy [piece]?"
        //   This is faster and avoids generating a full move list.
        // ────────────────────────────────────────────────────────────────────
        public static bool IsSquareAttackedBy(GameState state, Square target, PieceColor attackerColor)
        {
            // ── Attacked by a Pawn? ────────────────────────────────────────
            // Pawns attack diagonally forward (from attacker's perspective)
            int pawnDir = attackerColor == PieceColor.White ? -1 : +1;

            // The attacker pawn would be one rank "behind" the target
            // (behind = in the direction the attacking pawn came FROM)
            // White pawn attacks upward (negative direction), so it would
            // sit at target.Row + 1 (one row below the target)
            int pawnRow = target.Row - pawnDir; // row the pawn would stand on
            foreach (int dc in new[] { -1, +1 })
            {
                int pc = target.Col + dc;
                if (MoveGenerator.IsInBounds(pawnRow, pc))
                {
                    Piece p = state.GetPiece(pawnRow, pc);
                    if (p.Type == PieceType.Pawn && p.Color == attackerColor)
                        return true;
                }
            }

            // ── Attacked by a Knight? ──────────────────────────────────────
            (int, int)[] knightJumps =
            {
                (-2,-1), (-2,+1), (-1,-2), (-1,+2),
                (+1,-2), (+1,+2), (+2,-1), (+2,+1)
            };
            foreach (var (dr, dc) in knightJumps)
            {
                int r = target.Row + dr, c = target.Col + dc;
                if (MoveGenerator.IsInBounds(r, c))
                {
                    Piece p = state.GetPiece(r, c);
                    if (p.Type == PieceType.Knight && p.Color == attackerColor)
                        return true;
                }
            }

            // ── Attacked by Bishop or Queen (diagonals)? ──────────────────
            (int, int)[] diagonals = { (-1,-1), (-1,+1), (+1,-1), (+1,+1) };
            if (IsAttackedBySlider(state, target, attackerColor,
                    diagonals, PieceType.Bishop)) return true;

            // ── Attacked by Rook or Queen (straights)? ────────────────────
            (int, int)[] straights = { (-1,0), (+1,0), (0,-1), (0,+1) };
            if (IsAttackedBySlider(state, target, attackerColor,
                    straights, PieceType.Rook)) return true;

            // ── Attacked by King? ──────────────────────────────────────────
            (int, int)[] kingSteps =
            {
                (-1,-1), (-1,0), (-1,+1),
                ( 0,-1),         ( 0,+1),
                (+1,-1), (+1,0), (+1,+1)
            };
            foreach (var (dr, dc) in kingSteps)
            {
                int r = target.Row + dr, c = target.Col + dc;
                if (MoveGenerator.IsInBounds(r, c))
                {
                    Piece p = state.GetPiece(r, c);
                    if (p.Type == PieceType.King && p.Color == attackerColor)
                        return true;
                }
            }

            return false;
        }

        // ── Slide outward in given directions; look for Bishop/Rook/Queen ─
        // sliderType = Bishop → diagonals; Rook → straights
        // Queen matches BOTH calls
        private static bool IsAttackedBySlider(
            GameState state, Square target, PieceColor attackerColor,
            (int, int)[] directions, PieceType sliderType)
        {
            foreach (var (dr, dc) in directions)
            {
                int r = target.Row + dr;
                int c = target.Col + dc;

                while (MoveGenerator.IsInBounds(r, c))
                {
                    Piece p = state.GetPiece(r, c);

                    if (!p.IsEmpty)
                    {
                        // Found a piece along this ray
                        if (p.Color == attackerColor &&
                            (p.Type == sliderType || p.Type == PieceType.Queen))
                            return true;

                        break; // Blocked by any piece — stop sliding
                    }

                    r += dr;
                    c += dc;
                }
            }

            return false;
        }

        // ────────────────────────────────────────────────────────────────────
        // GAME STATE CHECKS
        // ────────────────────────────────────────────────────────────────────

        // Is the current player in checkmate?
        public static bool IsCheckmate(GameState state)
        {
            // Checkmate = in check AND no legal moves
            return IsKingInCheck(state, state.CurrentTurn)
                && GetAllLegalMoves(state).Count == 0;
        }

        // Is the current player stalemated?
        public static bool IsStalemate(GameState state)
        {
            // Stalemate = NOT in check AND no legal moves
            return !IsKingInCheck(state, state.CurrentTurn)
                && GetAllLegalMoves(state).Count == 0;
        }

        // Is the game a draw by the 50-move rule?
        public static bool IsFiftyMoveRule(GameState state) =>
            state.HalfMoveClock >= 100; // 50 full moves = 100 half-moves

        // ────────────────────────────────────────────────────────────────────
        // UTILITY
        // ────────────────────────────────────────────────────────────────────
        private static PieceColor Opponent(PieceColor color) =>
            color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }
}