# PVZ-Unity

A multimedia multiplayer strategy game inspired by Plants vs. Zombies, built with Unity and Netcode for GameObjects.

## Overview
This project is a networked multiplayer implementation where players can take on the role of either the **Plants** (Defenders) or **Zombies** (Attackers) in a 1v1 battle.

## Prerequisites
- **Unity Version**: Unity 2022.3 (LTS) or higher recommended.
- **Packages**:
  - Netcode for GameObjects
  - PlayFab SDK (for authentication)
  - TextMeshPro

## Installation
1. Clone or download this repository.
2. Open **Unity Hub** and add the project folder.
3. Open the project. Unity will automatically resolve and download the required packages.
4. If prompted, import the `TextMeshPro` essentials.

## Getting Started
To play the game, you must start from the **Login Scene**:

1. Open the scene: `Assets/Scenes/LoginScene.unity`
2. Press **Play** in the Unity Editor.
3. **Login**: Enter your credentials or play as a guest (if enabled).

## How to Play

### Game Flow
1. **Lobby**: After logging in, you will enter the lobby.
   - One player **Hosts** the game.
   - The other player **Joins** using the room code or matchmaking.
2. **Role Selection**:
   - Players choose their side: **Plants** or **Zombies**.
3. **Loading**: Once both players are ready, the game transitions to the arena.

### Controls
The game is primarily played using the **Mouse**.
- **Left Click**: Select cards, place units, interact with UI.

### Roles & Mechanics

#### 🌱 Team Plants (Defenders)
- **Goal**: Protect your house from the zombie waves.
- **Economy**: Collect **Sun** that falls from the sky or is produced by Sunflowers.
- **Gameplay**:
  - Select plant cards from your inventory (Seed Packets).
  - Place plants on the grid tiles.
  - **Fusion**: Combine compatible plants on the grid to create stronger versions (e.g., place a plant on top of another compatible plant).
  - **Shovel**: Use the shovel tool to remove existing plants and free up tiles.

#### 🧟 Team Zombies (Attackers)
- **Goal**: Reach the other side of the lawn and eat the brains.
- **Gameplay**:
  - Select zombie units to spawn.
  - Stratigically place zombies to overwhelm the plant defenses.
  - Specialized zombies (e.g., Disco Zombie) have unique abilities.

## Project Structure
- `Assets/Scripts/Networking`: Core networking logic (Host/Client, Game State).
- `Assets/Scripts/Plants`: Logic for individual plants.
- `Assets/Scripts/Zombies`: Logic for individual zombies.
- `Assets/Scripts/UI`: User Interface management (Lobby, Selection, Game HUD).

## Troubleshooting
- **Connection Issues**: Ensure both players are on the same network region or using the correct room code.
- **Missing Sounds**: Check `Assets/Scripts/Utilities/SoundManager.cs` to ensure audio clips are assigned.
