using System;
using System.Collections.Generic;
using System.Linq;

public class FEODiagram
{
    public List<FEOComponent> Components { get; set; }
    public List<FEOArrow> Arrows { get; set; }

    public FEODiagram()
    {
        Components = new List<FEOComponent>();
        Arrows = new List<FEOArrow>();
    }

    // Простой автолейаут по уровням
    public void AutoLayout(double startX = 100, double startY = 100, double layerStepY = 120, double blockStepX = 250)
    {
        var levels = new Dictionary<FEOComponent, int>();
        var visiting = new HashSet<FEOComponent>();

        int Depth(FEOComponent c)
        {
            if (levels.ContainsKey(c)) return levels[c];
            if (!c.Inputs.Any()) { levels[c] = 0; return 0; }
            if (visiting.Contains(c))
            {
                // Защита: цикл найден!
                levels[c] = 0;
                return 0;
            }
            visiting.Add(c);
            int d = 0;
            foreach (var inp in c.Inputs)
            {
                if (inp.From != null)
                    d = Math.Max(d, Depth(inp.From) + 1);
            }
            levels[c] = d;
            visiting.Remove(c);
            return d;
        }

        foreach (var cmp in Components) Depth(cmp);

        var byLevel = Components.GroupBy(c => levels[c])
                                .OrderBy(g => g.Key)
                                .ToList();

        for (int lvl = 0; lvl < byLevel.Count; lvl++)
        {
            var group = byLevel[lvl].ToList();
            for (int k = 0; k < group.Count; k++)
            {
                group[k].X = startX + k * blockStepX;
                group[k].Y = startY + lvl * layerStepY;
            }
        }
    }
}
