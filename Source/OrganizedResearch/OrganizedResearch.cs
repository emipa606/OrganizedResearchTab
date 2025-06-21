using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RimWorld;
using Verse;

namespace OrganizedResearch;

public class OrganizedResearch : MainTabWindow_Research
{
    private const int MaxWidth = 9;

    private const int MaxOriginalWidth = 6;

    private const float YStep = 0.63f;

    private const float XStep = 1f;

    private static List<List<ResearchProjectDef>> layers;

    public OrganizedResearch()
    {
        if (layers != null)
        {
            return;
        }

        var topologicalOrder = DefDatabase<ResearchProjectDef>.AllDefsListForReading.ListFullCopy();
        var groups = topologicalOrder.GroupBy(res => res.tab, res => res);

        foreach (var group in groups)
        {
            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                organizeResearchTab(group.ToList());
                stopwatch.Stop();
                Log.Message($"{stopwatch.ElapsedMilliseconds}ms organizing Research Tab {group.Key}.");
            }
            catch (Exception ex)
            {
                Log.Error($"OrganizedResearch: unidentified error. {ex}");
            }
            finally
            {
                ResearchProjectDef.GenerateNonOverlappingCoordinates();
            }
        }
    }

    private void organizeResearchTab(List<ResearchProjectDef> topologicalOrder)
    {
        enforceTopologicalOrdering(topologicalOrder);
        var list = coffmanGrahamOrdering(topologicalOrder);
        var num = 0;
        layers = [new List<ResearchProjectDef>(MaxWidth)];
        while (list.Count > 0)
        {
            var researchProjectDef = list.Last();
            var layersContainsItem = false;
            IEnumerable<ResearchProjectDef> requiredByThis = researchProjectDef.requiredByThis;
            foreach (var item in requiredByThis ?? [])
            {
                if (layers[num].Contains(item))
                {
                    layersContainsItem = true;
                }
            }

            if (layers[num].Count >= MaxOriginalWidth || layersContainsItem)
            {
                num++;
                layers.Add(new List<ResearchProjectDef>(MaxWidth));
            }

            layers[num].Add(researchProjectDef);
            list.RemoveLast();
        }

        foreach (var researchProjectDefs in layers)
        {
            researchProjectDefs.Reverse();
        }

        layers.Reverse();
        for (var i = 1; i < layers.Count; i++)
        {
            for (var j = 0; j < layers[i].Count; j++)
            {
                if (layers[i][j].prerequisites != null || layers[i][j].requiredByThis != null ||
                    layers[i - 1].Count >= MaxWidth)
                {
                    continue;
                }

                layers[i - 1].Add(layers[i][j]);
                layers[i].Remove(layers[i][j]);
                j--;
            }
        }

        for (var k = 0; k < layers.Count - 1; k++)
        {
            for (var index = 0; index < layers[k].Count; index++)
            {
                var item2 = layers[k][index];
                ResearchProjectDef researchProjectDef2 = null;
                for (var l = 0; l < (item2.requiredByThis?.Count ?? 0); l++)
                {
                    for (var m = k + 2; m < layers.Count; m++)
                    {
                        if (!layers[m].Contains(item2.requiredByThis?[l]) ||
                            layers[k + 1].Count >= MaxWidth && researchProjectDef2 == null)
                        {
                            continue;
                        }

                        if (researchProjectDef2 == null)
                        {
                            var researchProjectDef3 = new ResearchProjectDef
                            {
                                requiredByThis = [],
                                defName = $"d{item2.defName}"
                            };
                            layers[k + 1].Insert(0, researchProjectDef3);
                            researchProjectDef2 = researchProjectDef3;
                        }

                        researchProjectDef2.requiredByThis.Add(item2.requiredByThis?[l]);
                        item2.requiredByThis?.Add(researchProjectDef2);
                    }
                }
            }
        }

        layers = vertexOrderingWithinLayers(layers);
        var num2 = 0f;
        for (var n = 0; n < layers.Count; n++)
        {
            var num3 = 0f;
            foreach (var item3 in layers[n])
            {
                item3.researchViewX = num2;
                item3.researchViewY = num3 + (0.315f * (n % 2));
                num3 += YStep;
            }

            num2 += XStep;
        }
    }

    private static void enforceTopologicalOrdering(List<ResearchProjectDef> topologicalOrder)
    {
        // Collection gets modified
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var index = 0; index < topologicalOrder.Count; index++)
        {
            var item = topologicalOrder[index];
            IEnumerable<ResearchProjectDef> prerequisites = item.prerequisites;
            foreach (var item2 in prerequisites ?? [])
            {
                var num = topologicalOrder.IndexOf(item2);
                var num2 = topologicalOrder.IndexOf(item);
                if (num > num2)
                {
                    swapInList(topologicalOrder, num, num2);
                }

                item2.requiredByThis ??= [];

                item2.requiredByThis.Add(item);
            }
        }
    }

    private static List<ResearchProjectDef> coffmanGrahamOrdering(List<ResearchProjectDef> topologicalOrder)
    {
        var list = new List<ResearchProjectDef>(topologicalOrder.Count);
        while (topologicalOrder.Count > 0)
        {
            var researchProjectDef = topologicalOrder.First();
            foreach (var item in topologicalOrder)
            {
                if (researchProjectDef == item)
                {
                    continue;
                }

                if (item.prerequisites == null)
                {
                    if (researchProjectDef.prerequisites == null)
                    {
                        if ((int)item.techLevel < (int)researchProjectDef.techLevel)
                        {
                            researchProjectDef = item;
                        }
                    }
                    else
                    {
                        researchProjectDef = item;
                    }

                    continue;
                }

                if (researchProjectDef.prerequisites == null)
                {
                    break;
                }

                var containsPrerequisite = true;
                foreach (var prerequisite in item.prerequisites)
                {
                    if (!list.Contains(prerequisite))
                    {
                        containsPrerequisite = false;
                    }
                }

                if (!(researchProjectDef.prerequisites != null && containsPrerequisite))
                {
                    continue;
                }

                var list2 = new List<int>();
                var list3 = new List<int>();
                foreach (var prerequisite2 in researchProjectDef.prerequisites)
                {
                    list2.Add(list.IndexOf(prerequisite2));
                }

                list2.Sort();
                list2.Reverse();
                foreach (var prerequisite3 in item.prerequisites)
                {
                    list3.Add(list.IndexOf(prerequisite3));
                }

                list3.Sort();
                list3.Reverse();
                int i;
                for (i = 0; i < list2.Count && i < list3.Count; i++)
                {
                    if (list2[i] > list3[i])
                    {
                        researchProjectDef = item;
                        break;
                    }

                    if (list2[i] < list3[i])
                    {
                        break;
                    }
                }

                if (i < list2.Count && i == list3.Count)
                {
                    researchProjectDef = item;
                }
            }

            list.Add(researchProjectDef);
            topologicalOrder.Remove(researchProjectDef);
        }

        return list;
    }

    private List<List<ResearchProjectDef>> vertexOrderingWithinLayers(List<List<ResearchProjectDef>> order)
    {
        var list = saveOrder(order);
        for (var i = 0; i < 50; i++)
        {
            weightedMedian(order, i);
            transpose(order);
            if (countTotalCrossings(order) < countTotalCrossings(list))
            {
                list = saveOrder(order);
            }
        }

        return list;
    }

    private static List<List<ResearchProjectDef>> saveOrder(List<List<ResearchProjectDef>> order)
    {
        var list = order.ListFullCopy();
        for (var i = 0; i < list.Count; i++)
        {
            list[i] = order[i].ListFullCopy();
        }

        return list;
    }

    private void weightedMedian(List<List<ResearchProjectDef>> order, int iteration)
    {
        if (iteration % 2 == 0)
        {
            for (var i = 1; i < order.Count; i++)
            {
                var array = new float[order[i].Count];
                for (var j = 0; j < order[i].Count; j++)
                {
                    array[j] = medianValue(order[i][j], order[i - 1], true);
                }

                sortLayer(order[i], [..array]);
            }

            return;
        }

        for (var num = order.Count - 2; num >= 0; num--)
        {
            var array2 = new float[order[num].Count];
            for (var k = 0; k < order[num].Count; k++)
            {
                array2[k] = medianValue(order[num][k], order[num + 1], false);
            }

            sortLayer(order[num], [..array2]);
        }
    }

    private float medianValue(ResearchProjectDef vertex, List<ResearchProjectDef> adjacentLayer, bool toTheLeft)
    {
        var array = !toTheLeft
            ? adjacentPositionsToTheRight(vertex, adjacentLayer)
            : adjacentPositionsToTheLeft(vertex, adjacentLayer);
        var num = array.Length;
        var num2 = num / 2;
        if (num == 0)
        {
            return -1f;
        }

        if (num % 2 == 1)
        {
            return array[num2];
        }

        if (num == 2)
        {
            return (array[0] + array[1]) / 2f;
        }

        float num3 = array[num2 - 1] - array[0];
        float num4 = array[num - 1] - array[num2];
        return ((array[num2 - 1] * num4) + (array[num2] * num3)) / (num3 + num4);
    }

    private static int[] adjacentPositionsToTheLeft(ResearchProjectDef vertex, List<ResearchProjectDef> adjacentLayer)
    {
        var list = new List<int>();
        for (var i = 0; i < adjacentLayer.Count; i++)
        {
            for (var j = 0; j < (adjacentLayer[i].requiredByThis?.Count ?? 0); j++)
            {
                if (adjacentLayer[i].requiredByThis?[j] != vertex)
                {
                    continue;
                }

                list.Add(i);
                break;
            }
        }

        return list.ToArray();
    }

    private static int[] adjacentPositionsToTheRight(ResearchProjectDef vertex, List<ResearchProjectDef> adjacentLayer)
    {
        var list = new List<int>();
        for (var i = 0; i < adjacentLayer.Count; i++)
        {
            for (var j = 0; j < (vertex.requiredByThis?.Count ?? 0); j++)
            {
                if (vertex.requiredByThis?[j] != adjacentLayer[i])
                {
                    continue;
                }

                list.Add(i);
                break;
            }
        }

        return list.ToArray();
    }

    private static void sortLayer(List<ResearchProjectDef> order, List<float> median)
    {
        for (var i = 0; i < order.Count; i++)
        {
            var index = i;
            if (median[i] == -1f)
            {
                continue;
            }

            for (var j = i + 1; j < order.Count; j++)
            {
                if (median[j] >= 0f && median[j] < median[index])
                {
                    index = j;
                }
            }

            var item = median[index];
            median.RemoveAt(index);
            median.Insert(i, item);
            var item2 = order[index];
            order.RemoveAt(index);
            order.Insert(i, item2);
        }
    }

    private void transpose(List<List<ResearchProjectDef>> order)
    {
        var continueIteration = true;
        while (continueIteration)
        {
            continueIteration = false;
            for (var i = 1; i < order.Count; i++)
            {
                for (var j = 0; j < order[i].Count - 1; j++)
                {
                    var num = countCrossingsBetweenLayers(order[i - 1], order[i]);
                    swapInList(order[i], j, j + 1);
                    if (num > countCrossingsBetweenLayers(order[i - 1], order[i]))
                    {
                        continueIteration = true;
                    }
                    else
                    {
                        swapInList(order[i], j, j + 1);
                    }
                }
            }
        }
    }

    private int countTotalCrossings(List<List<ResearchProjectDef>> order)
    {
        var num = 0;
        for (var i = 0; i < order.Count - 1; i++)
        {
            num += countCrossingsBetweenLayers(order[i], order[i + 1]);
        }

        return num;
    }

    private int countCrossingsBetweenLayers(List<ResearchProjectDef> layerA, List<ResearchProjectDef> layerB)
    {
        var num = 0;
        layerB.Reverse();
        for (var i = 1; i < layerA.Count; i++)
        {
            for (var j = 1; j < layerB.Count; j++)
            {
                if (layerA[i].requiredByThis?.Contains(layerB[j]) ?? false)
                {
                    num += countEdgesInRange(layerA, layerB, i - 1, j - 1);
                }
            }
        }

        layerB.Reverse();
        return num;
    }

    private static int countEdgesInRange(List<ResearchProjectDef> layerA, List<ResearchProjectDef> layerB,
        int layerIndexA, int layerIndexB)
    {
        var num = 0;
        if (layerIndexA < 0 || layerIndexB < 0)
        {
            return 0;
        }

        num += countEdgesInRange(layerA, layerB, layerIndexA, layerIndexB - 1);
        num += countEdgesInRange(layerA, layerB, layerIndexA - 1, layerIndexB);
        num -= countEdgesInRange(layerA, layerB, layerIndexA - 1, layerIndexB - 1);
        if (layerA[layerIndexA].requiredByThis?.Contains(layerB[layerIndexB]) ?? false)
        {
            num++;
        }

        return num;
    }

    private static void swapInList<T>(List<T> list, int indexA, int indexB)
    {
        (list[indexA], list[indexB]) = (list[indexB], list[indexA]);
    }
}