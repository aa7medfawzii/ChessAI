using System;
using System.Collections.Generic;
using ChessAI.Models;
using ChessAI.Logic;

namespace ChessAI.AI
{
    // ── Minimax Engine with Alpha-Beta Pruning ─────────────────────────────
    //
    // MINIMAX: The AI assumes both players play optimally.
    //   - Maximiser (White) picks the move with the HIGHEST score
    //   - Minimiser (Black) picks the move with the LOWEST score
    //   - They alternate until we reach the search depth limit
    //
    // ALPHA-BETA PRUNING: Cuts off branches that cannot affect the result.
    //   - Alpha: best score the Maximiser is guaranteed so far
    //   - Beta:  best score the Minimiser is guaranteed so far
    //   - If beta <= alpha → prune (the opponent won't allow this path)
    //
    // DEPTH 3 means: AI move → player reply → AI move (3 half-moves = 1.5 full moves)
    // ────────────────────────────────────────────────────────────────────────
    public class MinimaxEngine
    {
        // ── Config ─────────────────────────────────────────────────────────
        private readonly int _depth;

        // ── Diagnostics (visible in the UI sidebar) ────────────────────────
        public int NodesEvaluated { get; private set; }
        public int BranchesPruned { get; private set; }

        public MinimaxEngine(int depth = 3)
        {
            _depth = depth;
        }

        // ────────────────────────────────────────────────────────────────────
        // ENTRY POINT — call this after the human makes a move
        // Returns the best Move the AI found, or null if no moves available
        // ────────────────────────────────────────────────────────────────────
        public Move? GetBestMove(GameState state)
        {
            // Reset diagnostics for each search
            NodesEvaluated = 0;
            BranchesPruned = 0;

            Move? bestMove  = null;
            bool  isWhite   = state.CurrentTurn == PieceColor.White;

            // Start alpha-beta window wide open
            int alpha = int.MinValue + 1;   // +1 to avoid overflow on negation
            int beta  = int.MaxValue - 1;

            // We want the BEST score for whoever's turn it is
            int bestScore = isWhite ? int.MinValue + 1 : int.MaxValue - 1;

            // Get all legal moves for the current player
            List<Move> moves = MoveValidator.GetAllLegalMoves(state);

            // ── Move Ordering ──────────────────────────────────────────────
            // Evaluate captures first — they're more likely to cause cutoffs,
            // which makes alpha-beta pruning much more effective.
            moves = OrderMoves(state, moves);

            foreach (Move move in moves)
            {
                // Simulate the move on a clone
                GameState next = state.Clone();
                next.ApplyMove(move);

                // Recurse: opponent plays from the resulting position
                int score = Minimax(next, _depth - 1, alpha, beta, !isWhite);

                // White maximises, Black minimises
                if (isWhite)
                {
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMove  = move;
                    }
                    alpha = Math.Max(alpha, bestScore);
                }
                else
                {
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestMove  = move;
                    }
                    beta = Math.Min(beta, bestScore);
                }
            }

            return bestMove;
        }

        // ────────────────────────────────────────────────────────────────────
        // MINIMAX with ALPHA-BETA PRUNING (recursive)
        //
        // state        — current board position (cloned, safe to modify)
        // depth        — how many more half-moves to search
        // alpha        — best score Maximiser (White) can guarantee so far
        // beta         — best score Minimiser (Black) can guarantee so far
        // isMaximising — true if it's White's turn in this node
        // ────────────────────────────────────────────────────────────────────
        private int Minimax(GameState state, int depth, int alpha, int beta, bool isMaximising)
        {
            // ── Terminal conditions ────────────────────────────────────────

            // Checkmate: current player has no moves AND is in check
            if (MoveValidator.IsCheckmate(state))
            {
                // Losing player is whoever's turn it currently is
                // Return a very large loss score; subtract depth so the AI
                // prefers FASTER checkmates (winning sooner is better)
                return isMaximising
                    ? -20000 + ((_depth - depth))   // Black checkmated White
                    :  20000 - ((_depth - depth));   // White checkmated Black
            }

            // Stalemate or 50-move rule → draw = 0
            if (MoveValidator.IsStalemate(state) || MoveValidator.IsFiftyMoveRule(state))
                return 0;

            // Depth limit reached → evaluate the board statically
            if (depth == 0)
            {
                NodesEvaluated++;
                return Evaluator.Evaluate(state);
            }

            // ── Generate and order moves ───────────────────────────────────
            List<Move> moves = MoveValidator.GetAllLegalMoves(state);
            moves = OrderMoves(state, moves);

            if (isMaximising)
            {
                // ── MAXIMISER (White) ──────────────────────────────────────
                int maxScore = int.MinValue + 1;

                foreach (Move move in moves)
                {
                    GameState next = state.Clone();
                    next.ApplyMove(move);

                    int score = Minimax(next, depth - 1, alpha, beta, false);
                    maxScore  = Math.Max(maxScore, score);
                    alpha     = Math.Max(alpha, score);

                    // ── Alpha-Beta Cutoff ──────────────────────────────────
                    // The Minimiser above us already has a path with score ≤ beta.
                    // Since we just found something ≥ beta, the Minimiser will
                    // never choose this branch — prune it.
                    if (beta <= alpha)
                    {
                        BranchesPruned++;
                        break;
                    }
                }

                return maxScore;
            }
            else
            {
                // ── MINIMISER (Black) ──────────────────────────────────────
                int minScore = int.MaxValue - 1;

                foreach (Move move in moves)
                {
                    GameState next = state.Clone();
                    next.ApplyMove(move);

                    int score = Minimax(next, depth - 1, alpha, beta, true);
                    minScore  = Math.Min(minScore, score);
                    beta      = Math.Min(beta, score);

                    // ── Alpha-Beta Cutoff ──────────────────────────────────
                    // The Maximiser above us already has a path with score ≥ alpha.
                    // Since we just found something ≤ alpha, the Maximiser will
                    // never choose this branch — prune it.
                    if (beta <= alpha)
                    {
                        BranchesPruned++;
                        break;
                    }
                }

                return minScore;
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // MOVE ORDERING
        //
        // Better move ordering = more alpha-beta cutoffs = faster search.
        // Simple strategy: put captures first, ordered by MVV-LVA
        // (Most Valuable Victim – Least Valuable Attacker).
        //
        // Example: PxQ (pawn captures queen) scored higher than QxP
        // ────────────────────────────────────────────────────────────────────
        private List<Move> OrderMoves(GameState state, List<Move> moves)
        {
            moves.Sort((a, b) =>
            {
                int scoreA = MoveScore(state, a);
                int scoreB = MoveScore(state, b);
                return scoreB.CompareTo(scoreA); // Descending: best first
            });

            return moves;
        }

        private int MoveScore(GameState state, Move move)
        {
            int score = 0;
            Piece moving = state.GetPiece(move.From);
            Piece target = state.GetPiece(move.To);

            // MVV-LVA: reward capturing valuable pieces with cheap pieces
            if (!target.IsEmpty)
            {
                score += 10 * Evaluator.GetPieceValue(target.Type)
                            - Evaluator.GetPieceValue(moving.Type);
            }

            // Reward promotion
            if (move.PromotionPiece != PieceType.None && move.PromotionPiece != PieceType.Pawn)
                score += Evaluator.GetPieceValue(move.PromotionPiece);

            return score;
        }
    }
}