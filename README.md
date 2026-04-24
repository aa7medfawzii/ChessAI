# ♟ AI-Powered Chess Engine

A fully functional Chess Engine built as an **Intelligent Agent** using **Adversarial Search**.

## Algorithms Used
- **Minimax Algorithm** — depth-3 game tree search
- **Alpha-Beta Pruning** — eliminates up to 90% of branches
- **Heuristic Evaluation** — material value + piece-square tables + king safety
- **MVV-LVA Move Ordering** — maximizes pruning efficiency

## Tech Stack
| | |
|---|---|
| Language | C# (.NET 10) |
| UI | Windows Forms |
| Paradigm | Object-Oriented (OOP) |
| Threading | async / Task.Run |

## Project Structure
ChessAI/
├── Models/     → Piece, Square, GameState
├── Logic/      → MoveGenerator, MoveValidator
├── AI/         → Evaluator, MinimaxEngine
└── UI/         → BoardPanel, ChessForm

## How to Run
```bash
dotnet run
```

## Agent Type
Model-Based Reflex Agent operating in a Fully Observable,
Deterministic, Strategic, Discrete, Multi-Agent, Sequential, Static environment.

## Features
- Click-to-move with legal move highlighting
- Check detection (king glows red)
- Undo move
- Flip board
- Live AI stats (nodes evaluated, branches pruned)
- Checkmate / Stalemate / 50-move rule detection
