using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;

public class Atom
{
    public Vector3 Position;
    public Color Color;
    public float Radius;
}

public static class PdbReader
{
    public static string[] AtomSymbols = { "C", "H", "N", "O", "P", "S" };
    public static float[] AtomRadii = { 1.548f, 1.100f, 1.400f, 1.348f, 1.880f, 1.808f };
    public static Color[] AtomColors = 
    { 
        new Color(0.282f, 0.6f, 0.498f, 1f), 
        Color.white, 
        new Color(0.443f, 0.662f, 0.882f, 1f), 
        new Color(0.827f, 0.294f, 0.333f, 1f), 
        new Color(1f, 0.839f, 0.325f,1f),
        new Color(0.960f, 0.521f, 0.313f, 1f) 
    };
    
    public static List<Vector4> ReadPdbFile(string path)
    {
        var atoms = new List<Vector4>();

        foreach (var line in File.ReadAllLines(path))
        {
            if (line.StartsWith("ATOM"))
            {
                var split = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var position = split.Where(s => s.Contains(".")).ToList();

                string c = split[2][0].ToString();
                int symbol = Array.IndexOf(AtomSymbols, c);

                var atom = new Vector4();

                if (symbol < 0)
                {
                    Debug.Log("Atom symbol not found");
                    atom.Set(float.Parse(position[0]), float.Parse(position[1]), float.Parse(position[2]), 1.5f);
                }
                else
                {
                    atom.Set(float.Parse(position[0]), float.Parse(position[1]), float.Parse(position[2]), AtomRadii[symbol]);
                }

                atoms.Add(atom);
            }

            if (line.StartsWith("TER")) break;
        }

        // Find the bounding box of the molecule and align the molecule with the origin 
        Vector3 bbMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 bbMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        Vector3 bbCenter;

        foreach (Vector4 atom in atoms)
        {
            bbMin = Vector3.Min(bbMin, new Vector3(atom.x, atom.y, atom.z));
            bbMax = Vector3.Max(bbMax, new Vector3(atom.x, atom.y, atom.z));
        }

        bbCenter = bbMin + (bbMax - bbMin) * 0.5f;

        for (int i = 0; i < atoms.Count; i++)
        {
            atoms[i] -= new Vector4(bbCenter.x, bbCenter.y, bbCenter.z, 0);
        }

        Debug.Log("Loaded " + atoms.Count + " atoms.");

        return atoms;
    }
}

