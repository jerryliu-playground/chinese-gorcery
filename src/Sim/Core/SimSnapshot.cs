using System.Security.Cryptography;
using System.Text;
using ChineseGrocery.Sim.Save;

namespace ChineseGrocery.Sim.Core;

/// <summary>Canonical text of the whole sim state (the save file), for determinism checks.</summary>
public static class SimSnapshot
{
    public static string Describe(GameSim sim) => SaveGame.Write(sim);

    /// <summary>Short SHA-256 of <see cref="Describe"/>, handy for comparing runs.</summary>
    public static string Hash(GameSim sim) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Describe(sim))))[..16];
}
