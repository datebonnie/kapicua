using System;
using System.Linq;
using Kapicua.AI;
using Kapicua.Core;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace Kapicua.EditorTools
{
    /// <summary>
    /// Bulk AI-vs-AI simulation that asserts rules-engine invariants.
    /// Run from the menu (Kapicua > Simulate 1000 Matches) or call Run() directly.
    /// </summary>
    public static class RulesSimulator
    {
        [MenuItem("Kapicua/Simulate 1000 Matches")]
        public static void SimulateFromMenu() => Debug.Log(Run(1000, seed: 12345));

        public static string Run(int matchCount, int seed)
        {
            var rng = new Random(seed);
            int rounds = 0, blocked = 0, ties = 0, capicuas = 0;
            int[] matchWins = new int[2];
            long totalPoints = 0;
            int maxRoundsInMatch = 0;

            for (int m = 0; m < matchCount; m++)
            {
                var match = new MatchState();
                while (!match.IsOver)
                {
                    int starter = match.NextStartingPlayer;
                    var round = new RoundEngine(rng, match.IsFirstRound, starter);
                    int actualStarter = round.CurrentPlayer;
                    PlayRound(round);
                    Validate(round);

                    rounds++;
                    if (round.Result.Blocked) blocked++;
                    if (round.Result.WinningTeam < 0) ties++;
                    if (round.Result.Capicua) capicuas++;
                    totalPoints += round.Result.Points;
                    match.ApplyRound(round.Result, actualStarter);

                    if (match.RoundsPlayed > 200)
                        throw new Exception("Match failed to terminate after 200 rounds");
                }
                matchWins[match.WinningTeam]++;
                maxRoundsInMatch = Math.Max(maxRoundsInMatch, match.RoundsPlayed);
            }

            return $"Simulated {matchCount} matches, {rounds} rounds. " +
                   $"Team wins {matchWins[0]}/{matchWins[1]}. " +
                   $"Blocked {blocked} ({100.0 * blocked / rounds:F1}%), ties {ties}, " +
                   $"capicúas {capicuas} ({100.0 * capicuas / rounds:F1}%), " +
                   $"avg round points {(double)totalPoints / rounds:F1}, " +
                   $"longest match {maxRoundsInMatch} rounds. All invariants held.";
        }

        static void PlayRound(RoundEngine round)
        {
            int safety = 0;
            while (!round.IsOver)
            {
                if (++safety > 500) throw new Exception("Round failed to terminate");
                int p = round.CurrentPlayer;
                if (round.MustPass(p)) round.Pass(p);
                else round.Play(p, DominoAI.ChooseMove(round, p));
            }
        }

        static void Validate(RoundEngine round)
        {
            // Tile conservation: board + hands = the full 28-tile set, no duplicates.
            var seen = round.Board.Tiles.Select(pt => pt.Tile)
                .Concat(round.Hands.SelectMany(h => h))
                .ToList();
            if (seen.Count != 28 || seen.Distinct().Count() != 28)
                throw new Exception($"Tile conservation violated: {seen.Count} tiles, {seen.Distinct().Count()} distinct");

            // Chain integrity: adjacent halves must match.
            var tiles = round.Board.Tiles;
            for (int i = 0; i + 1 < tiles.Count; i++)
                if (tiles[i].RightValue != tiles[i + 1].LeftValue)
                    throw new Exception($"Chain broken between {tiles[i].Tile} and {tiles[i + 1].Tile}");

            var r = round.Result;
            if (r.WinningTeam >= 0)
            {
                if (r.Points < 0) throw new Exception("Negative points");
                if (!r.Blocked && round.Hands[r.WinningPlayer].Count != 0)
                    throw new Exception("Domino winner still holds tiles");
                int expected = 0;
                if (!r.Blocked)
                {
                    for (int p = 0; p < RoundEngine.PlayerCount; p++)
                        if (RoundEngine.TeamOf(p) != r.WinningTeam)
                            expected += round.Hands[p].Sum(t => t.PipSum);
                    if (r.Capicua) expected += GameRules.KAPICUA_BONUS;   // flat +30 house rule
                    if (r.Points != expected)
                        throw new Exception($"Score mismatch: got {r.Points}, expected {expected}");
                }
            }
            else if (!r.Blocked)
            {
                throw new Exception("Tie result on a non-blocked round");
            }
        }
    }
}
