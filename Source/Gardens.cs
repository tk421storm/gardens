using HarmonyLib;
using RimWorld;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Verse;
using Verse.AI;
using Verse.Sound;
using Verse.Noise;

namespace TKS_Gardens
{

    public static class Util
    {
        public static int GetSequenceHashCode<T>(this IList<T> sequence)
        {
            const int seed = 487;
            const int modifier = 31;

            unchecked
            {
                return sequence.Aggregate(seed, (current, item) =>
                    (current * modifier) + item.GetHashCode());
            }
        }
    }

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

    public class TKS_GardensSettings : ModSettings
    {
        public bool debugPrint = false;
        public bool allowAllPlants = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref debugPrint, "debugPrint");
            Scribe_Values.Look(ref allowAllPlants, "allowAllPlants");

            base.ExposeData();
        }
    }

    public class TKS_GardensMapComponent : MapComponent
    {
        private Dictionary<int, String> plantCache;

        public List<ThingDef> plantablePlants;

        public int plantablePlantsHash;

        public TKS_GardensMapComponent(Map map) : base(map)
        {
            this.map = map;

        }

        public override void FinalizeInit()
        {

            base.FinalizeInit();


            if (this.plantCache == null)
            {
                this.plantCache = new Dictionary<int, String>();
            }

            //get all plantable plants on map and hash it (check for mod updates)
            this.plantablePlants = this.availablePlants();

            this.plantablePlantsHash = plantablePlants.GetSequenceHashCode<ThingDef>();

        }

        public List<ThingDef> availablePlants()
        {
            List<ThingDef> defs = new List<ThingDef>();

            TKS_Gardens.DebugMessage("getting all plants for garden planting");

            bool allowAllPlants = LoadedModManager.GetMod<TKS_Gardens>().GetSettings<TKS_GardensSettings>().allowAllPlants;

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {

                if (def != null && def.IsWithinCategory(ThingCategoryDefOf.Plants))
                {
                    
                    bool allow = true;
                    if (!allowAllPlants && def.plant.sowMinSkill == 0)
                    {
                        allow = false;
                    }

                    if (allow && !defs.Contains(def)) { defs.Add(def); }
                }
            }

            return defs;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref this.plantCache, "plantCache", LookMode.Value);
        }

        public void AddToCache(IntVec3 loc, ThingDef plantDef)
        {
            TKS_Gardens.DebugMessage("caching " + plantDef.defName + " for "+loc.ToString());

            int cellIndex = CellIndicesUtility.CellToIndex(loc, this.map.Size.x);

            this.plantCache[cellIndex] = plantDef.defName;

        }

        public ThingDef GetFromCache(IntVec3 loc)
        {
            int cellIndex = CellIndicesUtility.CellToIndex(loc, this.map.Size.x);

            if (this.plantCache.ContainsKey(cellIndex))
            {
                ThingDef cachedPlant = ThingDef.Named(this.plantCache[cellIndex]);
               //TKS_Gardens.DebugMessage("returning cached plantDef " + cachedPlant.defName + " for " + loc.ToString());
                return cachedPlant;
            }

            return null;
        }

        public void ClearFromCache(IntVec3 loc)
        {
            int cellIndex = CellIndicesUtility.CellToIndex(loc, this.map.Size.x);

            if (this.plantCache.ContainsKey(cellIndex))
            {
                this.plantCache.Remove(cellIndex);
            }
        }
    }

    public class TKS_Gardens : Mod
    {
        TKS_GardensSettings settings;

        public static void DebugMessage(string message)
        {
            if (LoadedModManager.GetMod<TKS_Gardens>().GetSettings<TKS_GardensSettings>().debugPrint)
            {
                Log.Message(message);
            }
        }

        public TKS_Gardens(ModContentPack content) : base(content)
        {
            this.settings = GetSettings<TKS_GardensSettings>();
        }

        private string editBufferFloat;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.CheckboxLabeled("TKS_AllowAllPlants".Translate(), ref settings.allowAllPlants);

            listingStandard.CheckboxLabeled("TKSDebugPrint".Translate(), ref settings.debugPrint);
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "TKSGardensSettings".Translate();
        }
        /*
        public static List<ThingDef> GetGardenPlants(Map map)
        {
            List<ThingDef> gardenPlants = new List<ThingDef>();

            bool allowAllPlants = LoadedModManager.GetMod<TKS_Gardens>().GetSettings<TKS_GardensSettings>().allowAllPlants;

            foreach (ThingDef thingDef in map.Biome.AllWildPlants)
            {
                gardenPlants.Add(thingDef);
            }
            return gardenPlants;
        }
        */
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
            this.icon = Symbols.symbolGardens;
            //this.tutorTag = "ZoneAdd_Growing";
            //this.hotKey = KeyBindingDefOf.Misc2;
        }

        protected override Zone MakeNewZone()
        {
            //PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.GrowingFood, KnowledgeAmount.Total);
            return new Zone_Garden(Find.CurrentMap.zoneManager);
        }
    }

    public class Zone_Garden : Zone, IPlantToGrowSettable
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
            Scribe_Values.Look<bool>(ref this.allowSow, "allowSow", true, false);
            Scribe_Values.Look<bool>(ref this.allowCut, "allowCut", true, false);
        }

        public List<ThingDef> availablePlants()
        {
            return this.Map.GetComponent<TKS_GardensMapComponent>().plantablePlants;
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

            TKS_Gardens.DebugMessage("Returning " + plantDefToPlant.defName + " as plantToGrow");
            return plantDefToPlant;
        }
        
        public ThingDef GetPlantDefToGrow(IntVec3 loc, Map map)
        {
            //check the cache
            TKS_GardensMapComponent component = map.GetComponent<TKS_GardensMapComponent>();

            IEnumerable<ThingDef> allowedPlants = PlantFilter.AllowedThingDefs;

            ThingDef cachedDef = component.GetFromCache(loc);

            if (cachedDef != null)
            {
                //check that it's still allowed
                if (allowedPlants.Contains(cachedDef))
                {
                    //TKS_Gardens.DebugMessage("returning cached def "+cachedDef.defName+" for "+loc.ToString());
                    return cachedDef; 

                } else
                {
                    TKS_Gardens.DebugMessage("Cache contains plant no longer allowed, regenerating");
                }
            }

            //otherwise pick a plant def to return
           ThingDef plantToGrow = GetPlantDefToGrow();

            //cache it
            component.AddToCache(loc, plantToGrow);

            return plantToGrow;

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

                    //object plantCategory = null;

                    foreach (ThingDef thingDef in this.Map.Biome.AllWildPlants)
                    {
                        if (thingDef.plant.Sowable)
                        {
                            TKS_Gardens.DebugMessage(this.ToString()+" allowing " + thingDef);
                            this.plantFilter.SetAllow(thingDef, true);
                            //plantCategory = thingDef.category;

                        }
                    }

                    //this.plantFilter.DisplayRootCategory = new TreeNode_ThingCategory(ThingCategoryDefOf.Plants);
                }
                return this.plantFilter;
            }
        }

        public override IEnumerable<Gizmo> GetZoneAddGizmos()
        {
            yield return DesignatorUtility.FindAllowedDesignator<Designator_ZoneAdd_Garden>();
            yield break;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            //IEnumerator<Gizmo> enumerator = null;

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
        }

        public override IEnumerable<InspectTabBase> GetInspectTabs()
        {
            return Zone_Garden.ITabs;
        }

        public bool allowSow = true;

        public bool allowCut = true;

        private ThingFilter plantFilter;

        //private List<ThingDef> plantList;

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
                return (float)(40);
            }
        }

        public ITab_Garden()
        {
            this.size = ITab_Garden.WinSize;
            this.labelKey = "TKSTabPlants";
            //this.tutorTag = "Plants";
        }

        protected override void FillTab()
        {
            Rect rect = new Rect(0f, 0f, ITab_Garden.WinSize.x, ITab_Garden.WinSize.y).ContractedBy(10f);
            Widgets.BeginGroup(rect);

            this.DrawPlantFilter(0f, rect.width, rect.height-20.0f, gardenZone);
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
            //parentFilter.SetAllow(ThingCategoryDefOf.Plants, true, null, exceptedFilters());
            foreach(ThingDef def in gardenZone.availablePlants())
            {
                parentFilter.SetAllow(def, true);
            }
            parentFilter.allowedQualitiesConfigurable = false;
            parentFilter.allowedHitPointsConfigurable = false;
#if v1_4
            ThingFilterUI.DoThingFilterConfigWindow(rect, state, plantFilter, parentFilter, openMask, forceHiddenDefs, this.HiddenSpecialThingFilters(), true, null, map);
#else
            ThingFilterUI.DoThingFilterConfigWindow(rect, state, plantFilter, parentFilter, openMask, forceHiddenDefs, this.HiddenSpecialThingFilters(), true, false, false, null, map);
#endif
        }

        private IEnumerable<SpecialThingFilterDef> exceptedFilters()
        {
            //yield return SpecialThingFilterDefOf.TKS_GardenPlants;
            yield break;
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
            //yield return SpecialThingFilterDefOf.TKS_GardenPlants;
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

    [HarmonyPatch(typeof(WorkGiver_PlantsCut))]
    static class WorkGiver_PlantsCut_Patches
    {
        [HarmonyPatch(typeof(WorkGiver_PlantsCut), "JobOnThing")]
        [HarmonyPostfix]
        public static void JobOnThing_Postfix(Pawn pawn, Thing t, bool forced, ref Job __result)
        {
            if (__result != null)
            {
                LocalTargetInfo cutTarget = __result.targetA;

                IntVec3 cell = cutTarget.Cell;

                Zone_Garden gardenZone = GridsUtility.GetZone(cell, pawn.Map) as Zone_Garden;
                if (gardenZone != null)
                {
                    foreach (Designation designation in pawn.Map.designationManager.designationsByDef[DesignationDefOf.CutPlant])
                    {
                        if (designation.target==cutTarget)
                        {
                            TKS_Gardens.DebugMessage("clearing cached plant for "+(IntVec3)cutTarget+" due to player-designated cut");
                            TKS_GardensMapComponent component = pawn.Map.GetComponent<TKS_GardensMapComponent>();
                            component.ClearFromCache((IntVec3)cutTarget);
                        }
                    }
                }

            }
        }

    }

    [HarmonyPatch(typeof(PlantUtility))]
    static class PlantUtility_Patches
    {
        [HarmonyPatch(typeof(PlantUtility), "AdjacentSowBlocker")]
        [HarmonyPostfix]
        public static void AdjacentSowBlocker_Postfix(ThingDef plantDef, IntVec3 c, Map map, ref Thing __result)
        {

            //TKS_Gardens.DebugMessage("running adjacent sow blocker postfix");
            //ignore if on a garden zone
            Zone_Garden gardenZone = GridsUtility.GetZone(c, map) as Zone_Garden;

            if (gardenZone != null)
            {
                //TKS_Gardens.DebugMessage("ignoring adjacent sow blocker due to garden zone");
                __result = null;
            }
            else
            {
                Zone_Garden adjacentGardenZone = null;

                //need to check if adjacent cells are garden cells
                CellRect rect = new CellRect(c.x, c.z, 1, 1);

                foreach (IntVec3 cell in rect.AdjacentCells)
                {
                    //TKS_Gardens.DebugMessage("checking adjacent cell "+cell+"for garden zone");
                    adjacentGardenZone = GridsUtility.GetZone(cell, map) as Zone_Garden;
                    if (adjacentGardenZone != null)
                    {
                        TKS_Gardens.DebugMessage("ignoring adjacent sow blocker due to adjacent garden zone");
                        __result = null;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(PlantUtility), "CanNowPlantAt")]
        [HarmonyPostfix]
        public static void CanNowPlantAt_Postfix(ThingDef plantDef, IntVec3 c, Map map, bool canWipePlantsExceptTree, ref bool __result)
        {
            Zone_Garden gardenZone = GridsUtility.GetZone(c, map) as Zone_Garden;

            if (gardenZone != null)
            {
                //TKS_Gardens.DebugMessage("allow planting now due to garden zone");
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(HaulAIUtility))]
    static class HaulAIUtility_Patches
    {
        [HarmonyPatch(typeof(HaulAIUtility), "HaulablePlaceValidator")]
        [HarmonyPrefix]
        public static bool HaulablePlaceValidator_Prefix(Thing haulable, Pawn worker, IntVec3 c, ref bool __result)
        {
            if (haulable != null && haulable.def.BlocksPlanting(false) && worker.Map.zoneManager.ZoneAt(c) is Zone_Garden)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

        [HarmonyPatch(typeof(WorkGiver_Grower))]
    static class WorkGiver_Grower_Patches
    {
        [HarmonyPatch(typeof(WorkGiver_Grower), "CalculateWantedPlantDef")]
        [HarmonyPrefix]
        public static bool CalculateWantedPlantDef_Prefix(IntVec3 c, Map map, ref ThingDef __result)
        {
            //TKS_Gardens.DebugMessage("running calculate wanted plant def prefix");

            //check if it's a garden zone
            Zone_Garden gardenZone = GridsUtility.GetZone(c, map) as Zone_Garden;

            if (gardenZone != null)
            {
                //TKS_Gardens.DebugMessage("calculating wanted plant def for Garden Zone");

                __result = gardenZone.GetPlantDefToGrow(c, map);
                //TKS_Gardens.DebugMessage("returning " + __result.defName);
                return false;
            }

            return true;
        }

    }

    [HarmonyPatch(typeof(WorkGiver_GrowerSow))]
    static class WorkGiver_GrowerSow_Patches {

        private static bool AllowSow(Zone currentZone)
        {
            bool allowSow = false;

            Zone_Growing zone_growing = currentZone as Zone_Growing;
            Zone_Garden zone_garden = currentZone as Zone_Garden;

            if (zone_garden != null)
            {
                allowSow = zone_garden.allowSow;
            }
            else
            {
                allowSow = zone_growing.allowSow;
            }

            return allowSow;
        }

        private static ThingDef GetPlantDefToGrow(IntVec3 c, Map map, Zone currentZone)
        {
            ThingDef plantDef = null;

            Zone_Growing zone_growing = currentZone as Zone_Growing;
            Zone_Garden zone_garden = currentZone as Zone_Garden;

            if (zone_garden != null)
            {
                plantDef = zone_garden.GetPlantDefToGrow(c, map);
            }
            else
            {
                plantDef = zone_growing.GetPlantDefToGrow();
            }

            return plantDef;
        }

        //step 1 - cut current plant (if any)
        private static Job CutCurrentPlant(IntVec3 c, Map map, Zone currentZone, Pawn pawn, bool forced)
        {
            Job cutJob = null;

            Zone_Growing zone_growing = currentZone as Zone_Growing;
            Zone_Garden zone_garden = currentZone as Zone_Garden;

            Plant plant = c.GetPlant(map);
            if (plant != null)
            {
                bool AllowsPlant = false;
                bool AllowCut = false;

                if (zone_garden != null)
                {
                    AllowsPlant = zone_garden.PlantFilter.Allows(plant.def);
                    AllowCut = zone_garden.allowCut;
                } else
                {
                    AllowsPlant = zone_growing.GetPlantDefToGrow() == plant.def;
                    AllowCut = zone_growing.allowCut;
                }
                if (AllowsPlant)
                {
                    return cutJob;
                }
                else if (AllowCut && PlantUtility.PawnWillingToCutPlant_Job(plant, pawn) && pawn.CanReserveAndReach(plant, PathEndMode.Touch, Danger.Deadly, 1, -1, null, forced))
                {
                    TKS_Gardens.DebugMessage("creating job for garden zone to cut current plant at " + c.ToString());
                    cutJob = JobMaker.MakeJob(JobDefOf.CutPlant, plant);
                    return cutJob;
                }
            }

            return cutJob;
        }

        //step 2 - check for adjacent sow blocker cut
        private static Job AdjacentSowBlocker(ThingDef wantedPlantDef, IntVec3 c, Map map, Zone currentZone, Pawn pawn, bool forced)
        {
            Job cutJob = null;

            Thing thing2 = PlantUtility.AdjacentSowBlocker(wantedPlantDef, c, map);
            if (thing2 != null)
            {
                Plant plant2 = thing2 as Plant;
                if (plant2 != null)
                {
                    //dont cut blocking plants in garden zone
                    bool cutIt = true;

                    Zone otherZone = thing2.Position.GetZone(map);
                    if (otherZone != null && (otherZone is Zone_Garden || otherZone is Zone_Growing))
                    {
                        Zone_Garden otherGardenZone = otherZone as Zone_Garden;
                        if (otherGardenZone != null)
                        {
                            ThingDef otherPlantDef = otherGardenZone.GetPlantDefToGrow(thing2.Position, map);

                            TKS_Gardens.DebugMessage("querying if plant at " + thing2.Position + "( " + thing2.def.defName + ") is allowed in " + otherGardenZone.label);
                            if (otherPlantDef == thing2.def)
                            {
                                TKS_Gardens.DebugMessage("not cutting " + otherPlantDef.defName + " at " + thing2.Position);
                                cutIt = false;
                            }
                        }
                        else
                        {
                            Zone_Growing otherGrowingZone = otherZone as Zone_Growing;
                            if (otherGrowingZone.GetPlantDefToGrow() == thing2.def)
                            {
                                cutIt = false;
                            }
                        }
                    }

                    if (cutIt && pawn.CanReserveAndReach(plant2, PathEndMode.Touch, Danger.Deadly, 1, -1, null, forced) && !plant2.IsForbidden(pawn))
                    {
                        cutJob = CutCurrentPlant(plant2.Position, map, otherZone, pawn, forced);

                        if (cutJob != null)
                        {
                            TKS_Gardens.DebugMessage("creating job for garden zone to cut adjacent plant to make room for " + wantedPlantDef.ToString() + " at " + c.ToString());
                            return cutJob;
                        }

                    }
                }
            }

            return cutJob;
        }


        [HarmonyPatch(typeof(WorkGiver_GrowerSow), "JobOnCell")]
        [HarmonyPrefix]
        public static bool JobOnCell_Prefix(WorkGiver_GrowerSow __instance, Pawn pawn, IntVec3 c, ref Job __result, bool forced = false)
        {

            Map map = pawn.Map;

            //handle non-zone plantings somewhere else
            if (GridsUtility.GetZone(c, map) == null) { return true; }

            //check if it's a garden zone
            Zone_Garden gardenZone = GridsUtility.GetZone(c, map) as Zone_Garden;

            Zone_Garden adjacentGardenZone = null;

            if (gardenZone == null)
            {
                //need to check if adjacent cells are garden cells
                CellRect rect = new CellRect(c.x, c.z, 1, 1);

                foreach (IntVec3 cell in rect.AdjacentCells)
                {
                    //TKS_Gardens.DebugMessage("checking adjacent cell "+cell+"for garden zone");
                    adjacentGardenZone = GridsUtility.GetZone(cell, map) as Zone_Garden;
                    if (adjacentGardenZone != null)
                    {
                        break;
                    }
                }

                if (adjacentGardenZone == null)
                {
                    Zone_Growing growingZone = GridsUtility.GetZone(c, map) as Zone_Growing;
                    Traverse.Create(__instance).Field("wantedPlantDef").SetValue(growingZone.GetPlantDefToGrow());

                    //run original method instead
                    TKS_Gardens.DebugMessage(pawn.Name + " running original JobOnCell for " + c);
                    return true;
                }
            }

            //we need to do all further operations either for a garden zone or for a growing zone
            Zone currentZone = GridsUtility.GetZone(c, map);

            TKS_Gardens.DebugMessage(pawn.Name+" running patched JobOnCell for "+c+", zone "+currentZone.label);

            if (c.IsForbidden(pawn) || !AllowSow(currentZone))
            {
                __result = null;
                return false;
            }

            //check for cut plant
            Job cutJob = CutCurrentPlant(c, map, currentZone, pawn, forced);
            if (cutJob != null)
            {
                __result = cutJob;
                return false;
            }

            ThingDef wantedPlantDef = GetPlantDefToGrow(c, map, currentZone);

            //check for terrian that cannot be planted
            float num = wantedPlantDef.plant.fertilityMin;
            if (map.fertilityGrid.FertilityAt(c) < num)
            {
                JobFailReason.Is("UnderRequiredFertility".Translate(wantedPlantDef.plant.fertilityMin), __instance.def.label);
                __result = null;
                return false;
            }

            if (wantedPlantDef.plant.sowMinSkill > 0 && pawn.skills != null && pawn.skills.GetSkill(SkillDefOf.Plants).Level < wantedPlantDef.plant.sowMinSkill)
            {
                JobFailReason.Is("UnderAllowedSkill".Translate(wantedPlantDef.plant.sowMinSkill), __instance.def.label);
                __result = null;
                return false;
            }

            //check for snow (seems like planting in snow is a-ok)
            /*
            if (!PlantUtility.SnowAllowsPlanting(c, map))
            {
                JobFailReason.Is("SnowPreventsPlanting".Translate(), __instance.def.label);
                __result = null;
                return false;
            }
            */

            //check for growing season
            if (!PlantUtility.GrowthSeasonNow(c, map, true))
            {
                JobFailReason.Is("NotGrowingSeason".Translate(), __instance.def.label);
                __result = null;
                return false;
            }

            //check for adjacent sow blocker
            Job adjacentSowCut = AdjacentSowBlocker(wantedPlantDef, c, map, currentZone, pawn, forced);
            if (adjacentSowCut != null)
            {
                __result = adjacentSowCut;
                return false;
            }

            List<Thing> thingList = c.GetThingList(map);

            int j = 0;
            while (j < thingList.Count)
            {
                Thing thing3 = thingList[j];
                if (thing3.def == wantedPlantDef)
                {
                    __result = null;
                    return false;
                }
                if (thing3.def.BlocksPlanting(false))
                {
                    
                    if (!pawn.CanReserveAndReach(thing3, PathEndMode.Touch, Danger.Some))
                    {
                        __result = null;
                        return false;
                    }
                    
                    if (thing3.def.EverHaulable)
                    {
                        TKS_Gardens.DebugMessage("creating job for garden zone to haul aside for " + wantedPlantDef.ToString() + " at " + c.ToString());
                        __result = HaulAIUtility.HaulAsideJobFor(pawn, thing3);
                        return false;
                    }

                    JobFailReason.Is("CannotMoveThing".Translate(thing3.def.defName), __instance.def.label);
                    __result = null;
                    return false;
                }
                else
                {
                    j++;
                }
            }

            if (!pawn.CanReserveAndReach(c, PathEndMode.Touch, Danger.Some))
            {
                __result = null;
                return false;
            }

            TKS_Gardens.DebugMessage("creating job for garden zone to plant " + wantedPlantDef.ToString() + " at " + c.ToString());

            Job job = JobMaker.MakeJob(JobDefOf.Sow, c);
            job.plantDefToSow = wantedPlantDef;

            //TKS_Gardens.DebugMessage("returning job for garden zone: " + job.ToString());
            __result = job;
            return false;
        }

        [HarmonyPatch(typeof(WorkGiver_Grower), "PotentialWorkCellsGlobal")]
        [HarmonyPostfix]
        public static IEnumerable<IntVec3> PotentialWorkCellsGlobal_postfix(IEnumerable<IntVec3> __results, WorkGiver_Grower __instance, Pawn pawn)
        {
            foreach (var value in __results)
            {
                yield return value;
            }

            TKS_Gardens.DebugMessage("checking for garden zones to plant");
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
                           //TKS_Gardens.DebugMessage("returning " + gardenCells.Count.ToString() + " garden cells for planting");
                            foreach (var value in gardenCells)
                            {
                                //wantedPlantDefField.SetValue(__instance, null);
                                Building edifice = value.GetFirstBuilding(pawn.Map);
                                if (edifice == null || !edifice.def.BlocksPlanting(false))
                                {
                                    yield return value;
                                }
                            }
                        }
                    }
                }
            }

            yield break;
        }
    }
}
