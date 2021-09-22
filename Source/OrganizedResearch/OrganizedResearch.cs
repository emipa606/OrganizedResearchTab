using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RimWorld;
using Verse;

namespace OrganizedResearch
{
    public class OrganizedResearch : MainTabWindow_Research
    {
        protected const int maxWidth = 9;

        protected const int maxOriginalWidth = 6;

        protected const float yStep = 0.63f;

        protected const float xStep = 1f;

        private static List<List<ResearchProjectDef>> _Layers;

        public OrganizedResearch()
        {
            var stopwatch = new Stopwatch();
            if (_Layers != null)
            {
                return;
            }

            try
            {
                stopwatch.Start();
                var topologicalOrder = DefDatabase<ResearchProjectDef>.AllDefsListForReading.ListFullCopy();
                organizeResearchTab(topologicalOrder);
                stopwatch.Stop();
                Log.Message(stopwatch.ElapsedMilliseconds + "ms organizing Research Tab.");
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

        protected void organizeResearchTab(List<ResearchProjectDef> topologicalOrder)
        {
            EnforceTopologicalOrdering(topologicalOrder);
            var list = CoffmanGrahamOrdering(topologicalOrder);
            var num = 0;
            _Layers = new List<List<ResearchProjectDef>> { new List<ResearchProjectDef>(9) };
            while (list.Count > 0)
            {
                var researchProjectDef = list.Last();
                var layersContainsItem = false;
                IEnumerable<ResearchProjectDef> requiredByThis = researchProjectDef.requiredByThis;
                foreach (var item in requiredByThis ?? Enumerable.Empty<ResearchProjectDef>())
                {
                    if (_Layers[num].Contains(item))
                    {
                        layersContainsItem = true;
                    }
                }

                if (_Layers[num].Count >= 6 || layersContainsItem)
                {
                    num++;
                    _Layers.Add(new List<ResearchProjectDef>(9));
                }

                _Layers[num].Add(researchProjectDef);
                list.RemoveLast();
            }

            foreach (var researchProjectDefs in _Layers)
            {
                researchProjectDefs.Reverse();
            }

            _Layers.Reverse();
            for (var i = 1; i < _Layers.Count; i++)
            {
                for (var j = 0; j < _Layers[i].Count; j++)
                {
                    if (_Layers[i][j].prerequisites != null || _Layers[i][j].requiredByThis != null ||
                        _Layers[i - 1].Count >= 9)
                    {
                        continue;
                    }

                    _Layers[i - 1].Add(_Layers[i][j]);
                    _Layers[i].Remove(_Layers[i][j]);
                    j--;
                }
            }

            for (var k = 0; k < _Layers.Count - 1; k++)
            {
                for (var index = 0; index < _Layers[k].Count; index++)
                {
                    var item2 = _Layers[k][index];
                    ResearchProjectDef researchProjectDef2 = null;
                    for (var l = 0; l < (item2.requiredByThis?.Count ?? 0); l++)
                    {
                        for (var m = k + 2; m < _Layers.Count; m++)
                        {
                            if (!_Layers[m].Contains(item2.requiredByThis?[l]) ||
                                _Layers[k + 1].Count >= 9 && researchProjectDef2 == null)
                            {
                                continue;
                            }

                            if (researchProjectDef2 == null)
                            {
                                var researchProjectDef3 = new ResearchProjectDef
                                {
                                    requiredByThis = new List<ResearchProjectDef>(),
                                    defName = "d" + item2.defName
                                };
                                _Layers[k + 1].Insert(0, researchProjectDef3);
                                researchProjectDef2 = researchProjectDef3;
                            }

                            researchProjectDef2.requiredByThis.Add(item2.requiredByThis?[l]);
                            item2.requiredByThis?.Add(researchProjectDef2);
                        }
                    }
                }
            }

            _Layers = VertexOrderingWithinLayers(_Layers);
            var num2 = 0f;
            for (var n = 0; n < _Layers.Count; n++)
            {
                var num3 = 0f;
                foreach (var item3 in _Layers[n])
                {
                    item3.researchViewX = num2;
                    item3.researchViewY = num3 + (0.315f * (n % 2));
                    num3 += 0.63f;
                }

                num2 += 1f;
            }
        }

        protected void EnforceTopologicalOrdering(List<ResearchProjectDef> topologicalOrder)
        {
            foreach (var item in topologicalOrder)
            {
                IEnumerable<ResearchProjectDef> prerequisites = item.prerequisites;
                foreach (var item2 in prerequisites ?? Enumerable.Empty<ResearchProjectDef>())
                {
                    var num = topologicalOrder.IndexOf(item2);
                    var num2 = topologicalOrder.IndexOf(item);
                    if (num > num2)
                    {
                        SwapInList(topologicalOrder, num, num2);
                    }

                    if (item2.requiredByThis == null)
                    {
                        item2.requiredByThis = new List<ResearchProjectDef>();
                    }

                    item2.requiredByThis.Add(item);
                }
            }
        }

        protected List<ResearchProjectDef> CoffmanGrahamOrdering(List<ResearchProjectDef> topologicalOrder)
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

        protected List<List<ResearchProjectDef>> VertexOrderingWithinLayers(List<List<ResearchProjectDef>> order)
        {
            var list = SaveOrder(order);
            for (var i = 0; i < 50; i++)
            {
                WeightedMedian(order, i);
                Transpose(order);
                if (CountTotalCrossings(order) < CountTotalCrossings(list))
                {
                    list = SaveOrder(order);
                }
            }

            return list;
        }

        protected List<List<ResearchProjectDef>> SaveOrder(List<List<ResearchProjectDef>> order)
        {
            var list = order.ListFullCopy();
            for (var i = 0; i < list.Count; i++)
            {
                list[i] = order[i].ListFullCopy();
            }

            return list;
        }

        protected void WeightedMedian(List<List<ResearchProjectDef>> Order, int iteration)
        {
            if (iteration % 2 == 0)
            {
                for (var i = 1; i < Order.Count; i++)
                {
                    var array = new float[Order[i].Count];
                    for (var j = 0; j < Order[i].Count; j++)
                    {
                        array[j] = MedianValue(Order[i][j], Order[i - 1], true);
                    }

                    SortLayer(Order[i], new List<float>(array));
                }

                return;
            }

            for (var num = Order.Count - 2; num >= 0; num--)
            {
                var array2 = new float[Order[num].Count];
                for (var k = 0; k < Order[num].Count; k++)
                {
                    array2[k] = MedianValue(Order[num][k], Order[num + 1], false);
                }

                SortLayer(Order[num], new List<float>(array2));
            }
        }

        protected float MedianValue(ResearchProjectDef vertex, List<ResearchProjectDef> adjacentLayer, bool toTheLeft)
        {
            var array = !toTheLeft
                ? AdjacentPositionsToTheRight(vertex, adjacentLayer)
                : AdjacentPositionsToTheLeft(vertex, adjacentLayer);
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

        protected int[] AdjacentPositionsToTheLeft(ResearchProjectDef vertex, List<ResearchProjectDef> adjacentLayer)
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

        protected int[] AdjacentPositionsToTheRight(ResearchProjectDef vertex, List<ResearchProjectDef> adjacentLayer)
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

        protected void SortLayer(List<ResearchProjectDef> Order, List<float> median)
        {
            for (var i = 0; i < Order.Count; i++)
            {
                var index = i;
                if (median[i] == -1f)
                {
                    continue;
                }

                for (var j = i + 1; j < Order.Count; j++)
                {
                    if (median[j] >= 0f && median[j] < median[index])
                    {
                        index = j;
                    }
                }

                var item = median[index];
                median.RemoveAt(index);
                median.Insert(i, item);
                var item2 = Order[index];
                Order.RemoveAt(index);
                Order.Insert(i, item2);
            }
        }

        protected void Transpose(List<List<ResearchProjectDef>> Order)
        {
            var continueIteration = true;
            while (continueIteration)
            {
                continueIteration = false;
                for (var i = 1; i < Order.Count; i++)
                {
                    for (var j = 0; j < Order[i].Count - 1; j++)
                    {
                        var num = CountCrossingsBetweenLayers(Order[i - 1], Order[i]);
                        SwapInList(Order[i], j, j + 1);
                        if (num > CountCrossingsBetweenLayers(Order[i - 1], Order[i]))
                        {
                            continueIteration = true;
                        }
                        else
                        {
                            SwapInList(Order[i], j, j + 1);
                        }
                    }
                }
            }
        }

        protected int CountTotalCrossings(List<List<ResearchProjectDef>> Order)
        {
            var num = 0;
            for (var i = 0; i < Order.Count - 1; i++)
            {
                num += CountCrossingsBetweenLayers(Order[i], Order[i + 1]);
            }

            return num;
        }

        protected int CountCrossingsBetweenLayers(List<ResearchProjectDef> layerA, List<ResearchProjectDef> layerB)
        {
            var num = 0;
            layerB.Reverse();
            for (var i = 1; i < layerA.Count; i++)
            {
                for (var j = 1; j < layerB.Count; j++)
                {
                    if (layerA[i].requiredByThis?.Contains(layerB[j]) ?? false)
                    {
                        num += CountEdgesInRange(layerA, layerB, i - 1, j - 1);
                    }
                }
            }

            layerB.Reverse();
            return num;
        }

        protected int CountEdgesInRange(List<ResearchProjectDef> layerA, List<ResearchProjectDef> layerB,
            int layerAindex, int layerBindex)
        {
            var num = 0;
            if (layerAindex < 0 || layerBindex < 0)
            {
                return 0;
            }

            num += CountEdgesInRange(layerA, layerB, layerAindex, layerBindex - 1);
            num += CountEdgesInRange(layerA, layerB, layerAindex - 1, layerBindex);
            num -= CountEdgesInRange(layerA, layerB, layerAindex - 1, layerBindex - 1);
            if (layerA[layerAindex].requiredByThis?.Contains(layerB[layerBindex]) ?? false)
            {
                num++;
            }

            return num;
        }

        private void SwapInList<T>(List<T> list, int indexA, int indexB)
        {
            (list[indexA], list[indexB]) = (list[indexB], list[indexA]);
        }

        private void PrintList(List<ResearchProjectDef> list)
        {
            foreach (var item in list)
            {
                Log.Message(item.defName);
            }
        }

        private void PrintTopologicalOrdering(List<ResearchProjectDef> list)
        {
            foreach (var item in list)
            {
                Log.Message(item.defName);
                IEnumerable<ResearchProjectDef> prerequisites = item.prerequisites;
                foreach (var item2 in prerequisites ?? Enumerable.Empty<ResearchProjectDef>())
                {
                    Log.Message("   |- " + item2.defName);
                }

                Log.Message("");
            }
        }

        private void PrintLayerAndTightEdges(List<ResearchProjectDef> Layer, List<ResearchProjectDef> nextLayer,
            int index)
        {
            Log.Message("Layer " + index);
            for (var i = 0; i < Layer.Count - 1; i++)
            {
                Log.Message(Layer[i].defName);
                IEnumerable<ResearchProjectDef> requiredByThis = Layer[i].requiredByThis;
                foreach (var item in requiredByThis ?? Enumerable.Empty<ResearchProjectDef>())
                {
                    if (nextLayer.Contains(item))
                    {
                        Log.Message("   |- " + item.defName);
                    }
                }

                Log.Message("");
            }
        }
    }
}