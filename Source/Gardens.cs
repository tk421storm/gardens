using HarmonyLib;
using RimWorld;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace TKS_Gardens
{
    /*
    [DefOf]
    class SpecialThingFilterDefOf
    {
        public static SpecialThingFilterDef NonEdiblePlants;
    }
    */
    [DefOf]
    class ThingCategoryDefOf
    {
        public static ThingCategoryDef Plants;
    }

    [StaticConstructorOnStartup]
    public static class InsertHarmony
    {
        static InsertHarmony()
        {
            Harmony harmony = new Harmony("TKS_Gardens");
            //Harmony.DEBUG = true;
            harmony.PatchAll();
            //Harmony.DEBUG = false;
            Log.Message($"TKS_Gardens: Patching finished");
        }
    }

    [StaticConstructorOnStartup]
    public static class Symbols
    {
        public static Texture2D symbolGardens;

        static Symbols()
        {
            symbolGardens = ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Gardens");
        }
    }
    /*
    public class SpecialThingFilterWorker_DecorPlants : SpecialThingFilterWorker
    {
        public override bool Matches(Thing t)
        {
            if (t == null) { return false; };

            CompRottable compRottable = t.TryGetComp<CompRottable>();
            if (compRottable == null)
            {
                return (!t.def.IsIngestible && !t.def.plant.Harvestable);
            } else
            {
                return !t.def.plant.Harvestable;
            }
        }

        public override bool CanEverMatch(ThingDef def)
        {
            return def.GetCompProperties<CompProperties_Rottable>() != null;
        }

        public override bool AlwaysMatches(ThingDef def)
        {
            CompProperties_Rottable compProperties = def.GetCompProperties<CompProperties_Rottable>();
            return (compProperties != null);
        }
    }
    */
    public class Zone_Garden : Zone , IPlantToGrowSettable
    {
        public override bool IsMultiselectable
        {
            get
            {
                return true;
            }
        }

        protected override Color NextZoneColor
        {
            get
            {
                return ZoneColorUtility.NextGrowingZoneColor();
            }
        }

        IEnumerable<IntVec3> IPlantToGrowSettable.Cells
        {
            get
            {
                return base.Cells;
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look<ThingFilter>(ref this.plantFilter, "animalFilter", Array.Empty<object>());
        }

        public ThingDef GetPlantDefToGrow()
        {
            //pick random plant from filter
            IEnumerable<ThingDef> allowedPlants = PlantFilter.AllowedThingDefs;

            if (allowedPlants.Count() == 0)
            {
                return null;
            }
            
            ThingDef plantDefToPlant = allowedPlants.RandomElement();

            //Log.Message("Returning " + plantDefToPlant.defName + " as plantToGrow");
            return plantDefToPlant;
        }

        public ThingDef GetPlantDefToGrow(IntVec3 loc, Map map)
        {
            //check if there's already a plant here that fits the filter
            Plant plantThere = loc.GetPlant(map);

            if (plantThere!=null)
            {
                if (PlantFilter.Allows(plantThere)) {
                    //Log.Message("returning def " + plantThere.def + " because it already fits the filter");
                    return plantThere.def;
                } else
                {
                    //Log.Warning("already a plant at "+loc.ToString()+", but it doesn't fit the filter");
                }
            }

            //otherwise pick a plant def to return
            return GetPlantDefToGrow();

        }

        public void SetPlantDefToGrow(ThingDef plantDef)
        {
            //nothing here
        }

        public bool CanAcceptSowNow()
        {
            return true;
        }

        public Zone_Garden()
        {
        }

        public Zone_Garden(ZoneManager zoneManager) : base("GardenZone".Translate(), zoneManager)
        {
        }

        public ThingFilter PlantFilter
        {
            get
            {
                if (this.plantFilter == null)
                {
                    this.plantFilter = new ThingFilter();

                    object plantCategory = null;

                    foreach (ThingDef thingDef in this.Map.Biome.AllWildPlants)
                    {
                        if (thingDef.plant.Sowable)
                        {
                            this.plantFilter.SetAllow(thingDef, true);
                            plantCategory = thingDef.category;

                        }
                    }

                    this.plantFilter.DisplayRootCategory = new TreeNode_ThingCategory(ThingCategoryDefOf.Plants);
                }
                return this.plantFilter;
            }
        }

        public override IEnumerable<Gizmo> GetZoneAddGizmos()
        {
            yield return DesignatorUtility.FindAllowedDesignator<TKS_Gardens.Designator_ZoneAdd_Garden>();
            yield break;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            IEnumerator<Gizmo> enumerator = null;

            yield return new Command_Toggle
            {
                defaultLabel = "CommandAllowSow".Translate(),
                defaultDesc = "CommandAllowSowDesc".Translate(),
                hotKey = KeyBindingDefOf.Command_ItemForbid,
                icon = TexCommand.ForbidOff,
                isActive = (() => this.allowSow),
                toggleAction = delegate ()
                {
                    this.allowSow = !this.allowSow;
                }
            };
            yield return new Command_Toggle
            {
                defaultLabel = "CommandAllowCut".Translate(),
                defaultDesc = "CommandAllowCutDesc".Translate(),
                icon = Designator_PlantsCut.IconTex,
                isActive = (() => this.allowCut),
                toggleAction = delegate ()
                {
                    this.allowCut = !this.allowCut;
                }
            };
            yield return new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("UI/Commands/CopySettings", true),
                defaultLabel = "CommandCopyZoneSettingsLabel".Translate(),
                defaultDesc = "CommandCopyZoneSettingsDesc".Translate(),
                action = delegate ()
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
                    PlantSettingsClipboard.Copy(PlantFilter);
                },
                hotKey = KeyBindingDefOf.Misc4
            };
            Command_Action command_Action = new Command_Action();
            command_Action.icon = ContentFinder<Texture2D>.Get("UI/Commands/PasteSettings", true);
            command_Action.defaultLabel = "CommandPasteZoneSettingsLabel".Translate();
            command_Action.defaultDesc = "CommandPasteZoneSettingsDesc".Translate();
            command_Action.action = delegate ()
            {
                SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
                PlantSettingsClipboard.PasteInto(PlantFilter);
            };
            command_Action.hotKey = KeyBindingDefOf.Misc5;
            if (!PlantSettingsClipboard.HasCopiedSettings)
            {
                command_Action.Disable(null);
            }
            yield return command_Action;

            yield break;
            yield break;
        }

        public override IEnumerable<InspectTabBase> GetInspectTabs()
        {
            return Zone_Garden.ITabs;
        }

        public bool allowSow = true;

        public bool allowCut = true;

        private ThingFilter plantFilter;

        private List<ThingDef> plantList;

        private static readonly ITab[] ITabs = new ITab[]
        {
            new ITab_Garden()
        };

    }

    public static class PlantSettingsClipboard
    {
        public static bool HasCopiedSettings
        {
            get
            {
                return PlantSettingsClipboard.copied;
            }
        }

        public static void Copy(ThingFilter s)
        {
            PlantSettingsClipboard.clipboard.CopyAllowancesFrom(s);
            PlantSettingsClipboard.copied = true;
        }

        // Token: 0x06006614 RID: 26132 RVA: 0x0022CE0C File Offset: 0x0022B00C
        public static void PasteInto(ThingFilter s)
        {
            s.CopyAllowancesFrom(PlantSettingsClipboard.clipboard);
        }

        // Token: 0x04003998 RID: 14744
        private static ThingFilter clipboard = new ThingFilter();

        // Token: 0x04003999 RID: 14745
        private static bool copied = false;
    }

    public class ITab_Garden : ITab
    {
        private float TopAreaHeight
        {
            get
            {
                return (float)(20);
            }
        }

        public ITab_Garden()
        {
            this.size = ITab_Garden.WinSize;
            this.labelKey = "TabPlants";
            //this.tutorTag = "Plants";
        }

        protected override void FillTab()
        {
            Rect rect = new Rect(0f, 0f, ITab_Garden.WinSize.x, ITab_Garden.WinSize.y).ContractedBy(10f);
            Widgets.BeginGroup(rect);

            this.DrawPlantFilter(0f, rect.width, rect.height, gardenZone);
            Widgets.EndGroup();
        }

        private void DrawPlantFilter(float curY, float width, float height, Zone_Garden gardenZone)
        {
            Rect rect = new Rect(0f, curY, width, height);
            ThingFilterUI.UIState state = this.plantFilterState;
            ThingFilter plantFilter = gardenZone.PlantFilter;
            //ThingFilter fixedAutoCutFilter = marker.parent.Map.animalPenManager.GetFixedAutoCutFilter();
            int openMask = 1;
            IEnumerable<ThingDef> forceHiddenDefs = null;
            Map map = gardenZone.Map;

            ThingFilter parentFilter = new ThingFilter();
            parentFilter.SetAllow(ThingCategoryDefOf.Plants, true);
            parentFilter.allowedQualitiesConfigurable = false;
            parentFilter.allowedHitPointsConfigurable = false;

            ThingFilterUI.DoThingFilterConfigWindow(rect, state, plantFilter, parentFilter, openMask, forceHiddenDefs, this.HiddenSpecialThingFilters(), true, null, map);
        }


        public override void Notify_ClickOutsideWindow()
        {
            base.Notify_ClickOutsideWindow();
            this.plantFilterState.quickSearch.Unfocus();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            this.plantFilterState.quickSearch.Reset();
        }

        private IEnumerable<SpecialThingFilterDef> HiddenSpecialThingFilters()
        {
            //yield return TKS_Gardens.SpecialThingFilterDefOf.NonEdiblePlants;
            yield break;
        }

        private ThingFilterUI.UIState plantFilterState = new ThingFilterUI.UIState();

        private static readonly Vector2 WinSize = new Vector2(300f, 480f);

        protected virtual Zone_Garden gardenZone
        {
            get
            {
                Zone thing = base.SelObject as Zone;
                if (thing != null)
                {
                    return base.SelObject as Zone_Garden;
                }

                return null;
            }
        }
    }


    public class Designator_ZoneAdd_Garden : Designator_ZoneAdd_Growing
    {
        protected override string NewZoneLabel
        {
            get
            {
                return "GardenZone".Translate();
            }
        }
        public Designator_ZoneAdd_Garden()
        {
            this.zoneTypeToPlace = typeof(Zone_Garden);
            this.defaultLabel = "GardenZone".Translate();
            this.defaultDesc = "DesignatorGardenZoneDesc".Translate();
            this.icon = TKS_Gardens.Symbols.symbolGardens;
            //this.tutorTag = "ZoneAdd_Growing";
            //this.hotKey = KeyBindingDefOf.Misc2;
        }

        protected override Zone MakeNewZone()
        {
            //PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.GrowingFood, KnowledgeAmount.Total);
            return new Zone_Garden(Find.CurrentMap.zoneManager);
        }
    }

    [HarmonyPatch(typeof(WorkGiver_Grower))]
    static class WorkGiver_Grower_Patches
    {
        [HarmonyPatch(typeof(WorkGiver_Grower), "CalculateWantedPlantDef")]
        [HarmonyPrefix]
        public static bool CalculateWantedPlantDef_Prefix(IntVec3 c, Map map, ref ThingDef __result)
        {
            //Log.Message("running calculate wanted plant def prefix");

            //check if it's a garden zone
            Zone_Garden gardenZone = GridsUtility.GetZone(c, map) as Zone_Garden;

            if (gardenZone != null)
            {
                //Log.Message("calculating wanted plant def for Garden Zone");

                __result = gardenZone.GetPlantDefToGrow(c, map);
                //Log.Message("returning " + __result.defName);
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(WorkGiver_GrowerSow), "JobOnCell")]
        [HarmonyPrefix]
        public static bool JobOnCell_Prefix(WorkGiver_GrowerSow __instance, Pawn pawn, IntVec3 c, ref Job __result, bool forced = false)
        {
            Map map = pawn.Map;
            if (c.IsForbidden(pawn))
            {
                __result = null;
                return false;
            }

            //check for terrian that cannot be planted
            float num = ThingDefOf.Plant_Potato.plant.fertilityMin;
            if (map.fertilityGrid.FertilityAt(c) < num)
            {
                __result = null;
                return false;
            }


            //check if it's a garden zone
            Zone_Garden gardenZone = GridsUtility.GetZone(c, map) as Zone_Garden;

            if (gardenZone != null)
            {
                //break out early if disabled sow
                if (!gardenZone.allowSow)
                {
                    __result = null;
                    return false;
                }

                //Log.Message("running JobOnCell for Garden Zone");
                List<Thing> thingList = c.GetThingList(map);
                for (int i = 0; i < thingList.Count; i++)
                {
                    Thing thing = thingList[i];
                    if (gardenZone.PlantFilter.Allows(thing.def))
                    {
                        __result = null;
                        return false;
                    } 
                }

                Plant plant = c.GetPlant(map);
                if (plant != null)
                {
                    if (gardenZone.PlantFilter.Allows(plant.def))
                    {
                        __result = null;
                        return false;
                    } else if (gardenZone.allowCut && PlantUtility.PawnWillingToCutPlant_Job(plant, pawn) && pawn.CanReserveAndReach(plant, PathEndMode.Touch, Danger.Deadly, 1, -1, null, forced))
                    {
                        //Log.Message("creating job for garden zone to cut current plant at " + c.ToString());
                        __result = JobMaker.MakeJob(JobDefOf.CutPlant, plant);
                        return false;
                    }
                }
    
                ThingDef wantedPlantDef = gardenZone.GetPlantDefToGrow();

                if (wantedPlantDef.plant.sowMinSkill > 0 && pawn.skills != null && pawn.skills.GetSkill(SkillDefOf.Plants).Level < wantedPlantDef.plant.sowMinSkill)
                {
                    JobFailReason.Is("UnderAllowedSkill".Translate(wantedPlantDef.plant.sowMinSkill), __instance.def.label);
                    __result = null;
                    return false;
                }

                Thing thing2 = PlantUtility.AdjacentSowBlocker(wantedPlantDef, c, map);
                if (thing2 != null)
                {
                    Plant plant2 = thing2 as Plant;
                    if (plant2 != null && pawn.CanReserveAndReach(plant2, PathEndMode.Touch, Danger.Deadly, 1, -1, null, forced) && !plant2.IsForbidden(pawn))
                    {
                        IPlantToGrowSettable plantToGrowSettable = plant2.Position.GetPlantToGrowSettable(plant2.Map);
                        if (plantToGrowSettable == null || plantToGrowSettable.GetPlantDefToGrow() != plant2.def)
                        {
                            Zone_Growing zone_Growing2 = c.GetZone(map) as Zone_Growing;
                            Zone_Growing zone_Growing3 = plant2.Position.GetZone(map) as Zone_Growing;
                            if ((zone_Growing2 != null && !zone_Growing2.allowCut) || (zone_Growing3 != null && !zone_Growing3.allowCut))
                            {
                                __result = null;
                                return false;
                            }
                            if (!PlantUtility.PawnWillingToCutPlant_Job(plant2, pawn))
                            {
                                __result = null;
                                return false;
                            }
                            //Log.Message("creating job for garden zone to cut adjacent plant to make room for " + wantedPlantDef.ToString() + " at " + c.ToString());
                            __result = JobMaker.MakeJob(JobDefOf.CutPlant, plant2);
                            return false;
                        }
                    }
                    __result = null;
                    return false;
                }

                int j = 0;
                while (j < thingList.Count)
                {
                    Thing thing3 = thingList[j];
                    if (thing3.def.BlocksPlanting(false))
                    {
                        if (!pawn.CanReserve(thing3, 1, -1, null, forced))
                        {
                            __result = null;
                            return false;
                        }
                        if (thing3.def.category == ThingCategory.Plant)
                        {
                            if (thing3.IsForbidden(pawn))
                            {
                                __result = null;
                                return false;
                            }
                            if (gardenZone.allowCut)
                            {
                                __result = null;
                                return false;
                            }
                            if (!PlantUtility.PawnWillingToCutPlant_Job(thing3, pawn))
                            {
                                __result = null;
                                return false;
                            }
                            //Log.Message("creating job for garden zone to cut current plant to make room for " + wantedPlantDef.ToString() + " at " + c.ToString());
                            __result = JobMaker.MakeJob(JobDefOf.CutPlant, thing3);
                            return false;
                        }
                        else
                        {
                            if (thing3.def.EverHaulable)
                            {
                                //Log.Message("creating job for garden zone to haul aside for " + wantedPlantDef.ToString() + " at " + c.ToString());
                                __result = HaulAIUtility.HaulAsideJobFor(pawn, thing3);
                                return false;
                            }
                            __result = null;
                            return false;
                        }
                    }
                    else
                    {
                        j++;
                    }
                }

                if (!pawn.CanReserve(c, 1, -1, null, forced) || !gardenZone.allowSow)
                {
                    __result = null;
                    return false;
                }

                //Log.Message("creating job for garden zone to plant " + wantedPlantDef.ToString() + " at " + c.ToString());

                Job job = JobMaker.MakeJob(JobDefOf.Sow, c);
                job.plantDefToSow = wantedPlantDef;

                //Log.Message("returning job for garden zone: " + job.ToString());
                __result = job;
                return false;
            }
            
            return true;
        }

        [HarmonyPatch(typeof(WorkGiver_Grower), "PotentialWorkCellsGlobal")]
        [HarmonyPostfix]
        public static IEnumerable<IntVec3> PotentialWorkCellsGlobal_postfix(IEnumerable<IntVec3> __results, WorkGiver_Grower __instance, Pawn pawn)
        {
            foreach (var value in __results)
            {
                yield return value;
            }

            //Log.Message("checking for garden zones to plant");
            Danger maxDanger = pawn.NormalMaxDanger();

            //cannot access this private field from IEnumerable postfix
            //FieldInfo wantedPlantDefField = __instance.GetType().GetField("wantedPlantDef", BindingFlags.NonPublic | BindingFlags.Instance);

            List<Zone> zonesList = pawn.Map.zoneManager.AllZones;

            for (int i = 0; i < zonesList.Count; i ++)
            {
                Zone_Garden growZone = zonesList[i] as Zone_Garden;
                if (growZone != null)
                {
                    List<IntVec3> gardenCells = new List<IntVec3>();

                    if (growZone.cells.Count == 0)
                    {
                        Log.ErrorOnce("Garden zone has 0 cells: " + growZone, -563487);
                    }
                    
                    else if (!growZone.ContainsStaticFire && pawn.CanReach(growZone.Cells[0], PathEndMode.OnCell, maxDanger, false, false, TraverseMode.ByPawn))
                    {
                        for (int j = 0; j < growZone.cells.Count; j ++)
                        {
                            gardenCells.Add(growZone.cells[j]);
                        }

                        if (gardenCells.Count>0)
                        {
                           //Log.Message("returning " + gardenCells.Count.ToString() + " garden cells for planting");
                            foreach (var value in gardenCells)
                            {
                                //wantedPlantDefField.SetValue(__instance, null);
                                yield return value;
                            }
                        }
                    }
                }
            }

            yield break;
            yield break;
        }
    }
}
