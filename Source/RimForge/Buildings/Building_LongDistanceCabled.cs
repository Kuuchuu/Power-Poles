using RimWorld;
using System.Collections.Generic;
using System.Threading.Tasks;
using RimForge.PolesSettings;
using UnityEngine;
using Verse;

namespace RimForge.Buildings
{
    public abstract class Building_LongDistanceCabled : Building_LongDistancePower
    {
        public static readonly Color DefaultCableColor = new Color(150 / 255f, 85 / 255f, 11 / 255f);
        private static readonly Dictionary<Color, Material> cableMaterialsCache = new Dictionary<Color, Material>();

        public static Material GetCableMaterial(Color color)
        {
            if (cableMaterialsCache.TryGetValue(color, out var found))
                return found;

            var mat = MaterialPool.MatFrom("RF/Buildings/PowerPoleCable", ShaderDatabase.Cutout, color);
            cableMaterialsCache.Add(color, mat);
            return mat;
        }

        public virtual bool IgnoreMaterialColor => true;
        public override Color DrawColor
        {
            get => IgnoreMaterialColor ? Color.white : base.DrawColor;
            set
            {
                if (IgnoreMaterialColor)
                    return;
                base.DrawColor = value;
            }
        }
        public override float MaxLinkDistance => PolesModSettings.CableMaxDistance;

        private readonly Dictionary<Building_LongDistanceCabled, List<Vector2>> connectionToPoints = new Dictionary<Building_LongDistanceCabled, List<Vector2>>();
        private readonly object key = new object();
        private Material cableMatCached;
        private float slack = 1f;
        private int colorInt;

        public static List<Vector2> GeneratePoints(Building_LongDistanceCabled poleA, Building_LongDistanceCabled poleB, Vector2 a, Vector2 b, Vector2 c, Vector2 d, List<Vector2> points = null)
        {
            if (poleA.DestroyedOrNull() || poleB.DestroyedOrNull())
                return points;

            points ??= new List<Vector2>(128);
            points.Clear();

            int pc = GetCablePointCount(a, d);
            if (pc < 3)
                pc = 3;
            
            for (int i = 0; i < pc; i++)
            {
                float t = (float)i / (pc - 1);
                Vector2 bezier = Effects.Bezier.Evaluate(t, a, b, c, d);
                points.Add(bezier);
            }

            return points;
        }

        public void GeneratePointsAsync(Building_LongDistanceCabled dom, Building_LongDistanceCabled sub)
        {
            if (dom.DestroyedOrNull() || sub.DestroyedOrNull())
                return;

            dom.connectionToPoints[sub] = null;

            GetSagAndDistanceToMidpoint(out float sag, out float pctToOtherSide);

			Vector2 start = dom.GetFlatConnectionPoint();
			Vector2 end = sub.GetFlatConnectionPoint();

		    Vector2 midA = Vector2.Lerp(start, end, pctToOtherSide);
		    Vector2 p1 = midA + new Vector2(0, sag);

		    Vector2 midB = Vector2.Lerp(start, end, 1f - pctToOtherSide);
		    Vector2 p2 = midB + new Vector2(0, sag);

			lock (key)
            {
				Task.Run(() =>
				{
					var list = GeneratePoints(dom, sub, start, p1, p2, end);
					dom.connectionToPoints[sub] = list;
				});
			}
        }

        private void GetSagAndDistanceToMidpoint(out float sag, out float pctToOtherSide)
        {
            sag = slack * -1.2f;
            pctToOtherSide = slack * 0.23f;
        }
        
        public static int GetCablePointCount(Vector2 a, Vector2 b)
        {
            return Mathf.Clamp(Mathf.RoundToInt((a - b).magnitude * PolesModSettings.CableSegmentsPerCell), 10, 100);
        }

        /// <summary>
        /// Gets the point that the cables should link up to.
        /// By default simply returns the draw position, however overriding this method and adding an offset
        /// to match the graphics and current rotation is probably a good idea.
        /// </summary>
        /// <returns>The world space connection point. Note that it is a flat Vector2, not a Vector3.</returns>
        public virtual Vector2 GetFlatConnectionPoint()
        {
            return DrawPos.WorldToFlat();
        }

        public virtual Color GetCableColor()
        {
            return colorInt switch
            {
                1 => new Color32(140, 160, 160, 255),
                2 => new Color32(232, 221, 63, 255),
                3 => new Color32(35, 33, 43, 255),
                _ => DefaultCableColor
            };
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            yield return new Command_Action
            {
                defaultLabel = "Change cable color",
                defaultDesc = "Changes the cable color.",
                action = () =>
                {
                    int[] objects =
					[
						0,
                        1,
                        2,
                        3
                    ];
					string[] optionNames =
					[
						"Copper",
                        "Tin",
                        "Gold",
                        "Rubber"
                    ];

                    FloatMenuUtility.MakeMenu(objects, i => optionNames[i], i => () =>
                    {
                        if (colorInt != i)
                        {
                            List<object> selectedObjectsListForReading = Find.Selector.SelectedObjectsListForReading;
                            if (selectedObjectsListForReading != null)
                            {
                                foreach (object obj in selectedObjectsListForReading)
                                {
                                    if (obj is Building_LongDistanceCabled building)
                                    {
                                        building.colorInt = i;
                                        building.UpdateCableColor();
                                    }
                                }
                            }
                        }
                    });
                },
                icon = PolesContent.SwapIcon
            };

            yield return new Command_Action
            {
				defaultLabel = $"Change cable slack",
				defaultDesc = "Allows adjusting how far down the cable sags.\nThis is a visual change only.",
				action = () =>
				{
                    float[] values = new float[11];

                    for (int i = 0; i < 11; i++)
                    {
                        values[i] = (float)i / 5;
                    }

					FloatMenuUtility.MakeMenu(values, p => $"<color={(p == slack ? "green" : "white")}>{p:P0}</color>", pct => () =>
					{
                        if (slack == pct)
                            return;

					    List<object> selectedObjectsListForReading = Find.Selector.SelectedObjectsListForReading;
                        if (selectedObjectsListForReading != null)
                        {
                            foreach (object obj in selectedObjectsListForReading)
                            {
                                if (obj is not Building_LongDistanceCabled cbl)
                                    continue;

								cbl.slack = pct;
								cbl.RegenerateCables();
						    }
                        }

					});
				},
				icon = PolesContent.SlackIcon,
                groupable = true
			};
        }

        public void UpdateCableColor()
        {
            cableMatCached = GetCableMaterial(GetCableColor());
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            base.DynamicDrawPhaseAt(phase, drawLoc, flip);

            if (phase == DrawPhase.Draw)
                DrawInt();
        }

        private void DrawInt()
        {
            if (connectionToPoints == null)
                return;

            if (cableMatCached == null)
                UpdateCableColor();

            foreach (var pair in connectionToPoints)
            {
                if (pair.Key.DestroyedOrNull())
                    continue;

                var points = pair.Value;
                if (points == null || points.Count < 2)
                    continue;

                float height = AltitudeLayer.Skyfaller.AltitudeFor();
                for (int i = 1; i < points.Count; i++)
                {
                    var last = points[i - 1].FlatToWorld(height);
                    var current = points[i].FlatToWorld(height);

                    GenDraw.DrawLineBetween(last, current, cableMatCached, PolesModSettings.CableThickness);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref colorInt, "cableColor");
            Scribe_Values.Look(ref slack, "slack", 1f);
        }

        protected override void PreLinksReset()
        {
            base.PreLinksReset();

            connectionToPoints.Clear();
        }

        protected override void UponLinkAdded(Building_LongDistancePower to, bool isOwner)
        {
            base.UponLinkAdded(to, isOwner);

            if (!isOwner)
                return;

            if(to is Building_LongDistanceCabled cabled)
                GeneratePointsAsync(this, cabled);
        }

        protected override void UponLinkRemoved(Building_LongDistancePower from, bool isOwner)
        {
            base.UponLinkRemoved(from, isOwner);

            if (!isOwner)
                return;

            if (from is Building_LongDistanceCabled cabled && connectionToPoints.ContainsKey(cabled))
                connectionToPoints.Remove(cabled);
        }

        protected virtual void RegenerateCables()
        {
            connectionToPoints.Clear();

            foreach (var conn in OwnedConnectionsSanitized)
            {
                if (conn is Building_LongDistanceCabled cabled)
                    GeneratePointsAsync(this, cabled);
            }
        }
	}
}
