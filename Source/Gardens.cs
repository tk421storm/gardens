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
                return ThingDef.Named(this.plantCache[cellIndex]);
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

            ThingFilterUI.DoThingFilterConfigWindow(rect, state, plantFilter, parentFilter, openMask, forceHiddenDefs, this.HiddenSpecialThingFilters(), true, null, map);
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

        [HarmonyPatch(typeof(WorkGiver_GrowerSow), "JobOnCell")]
        [HarmonyPrefix]
        public static bool JobOnCell_Prefix(WorkGiver_GrowerSow __instance, Pawn pawn, IntVec3 c, ref Job __result, bool forced = false)
        {

            Map map = pawn.Map;

            //check if it's a garden zone
            Zone_Garden gardenZone = GridsUtility.GetZone(c, map) as Zone_Garden;

            if (gardenZone == null)
            {
                //run original method instead
                return true;
            }

            if (c.IsForbidden(pawn) || !gardenZone.allowSow)
            {
                __result = null;
                return false;
            }

            Plant plant = c.GetPlant(map);
            if (plant != null)
            {
                if (gardenZone.PlantFilter.Allows(plant.def))
                {
                    __result = null;
                    return false;
                }
                else if (gardenZone.allowCut && PlantUtility.PawnWillingToCutPlant_Job(plant, pawn) && pawn.CanReserveAndReach(plant, PathEndMode.Touch, Danger.Deadly, 1, -1, null, forced))
                {
                    TKS_Gardens.DebugMessage("creating job for garden zone to cut current plant at " + c.ToString());
                    __result = JobMaker.MakeJob(JobDefOf.CutPlant, plant);
                    return false;
                }
            }

            ThingDef wantedPlantDef = gardenZone.GetPlantDefToGrow(c, map);

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
                        Zone_Garden otherGardenZone = (Zone_Garden)otherZone;
                        if (otherGardenZone != null)
                        {
                            if (otherGardenZone.GetPlantDefToGrow(thing2.Position, map) == thing2.def)
                            {
                                cutIt = false;
                            }
                        } else
                        {
                            Zone_Growing otherGrowingZone = (Zone_Growing)otherZone;
                            if (otherGrowingZone.GetPlantDefToGrow() == thing2.def)
                            {
                                cutIt = false;
                            }
                        }
                    }

                    if (cutIt && pawn.CanReserveAndReach(plant2, PathEndMode.Touch, Danger.Deadly, 1, -1, null, forced) && !plant2.IsForbidden(pawn))
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
                                JobFailReason.Is("RefusesPlantCut".Translate(plant2.def.defName, pawn.Name), __instance.def.label);
                                __result = null;
                                return false;
                            }

                            TKS_Gardens.DebugMessage("creating job for garden zone to cut adjacent plant to make room for " + wantedPlantDef.ToString() + " at " + c.ToString());
                            __result = JobMaker.MakeJob(JobDefOf.CutPlant, plant2);
                            return false;
                        }
                    }
                    if (cutIt)
                    {
                        __result = null;
                        return false;
                    }
                }
            }

            List<Thing> thingList = c.GetThingList(map);

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
                            JobFailReason.Is("RefusesPlantCut".Translate(thing3.def.defName, pawn.Name), __instance.def.label);
                            __result = null;
                            return false;
                        }
                        TKS_Gardens.DebugMessage("creating job for garden zone to cut current plant to make room for " + wantedPlantDef.ToString() + " at " + c.ToString());
                        __result = JobMaker.MakeJob(JobDefOf.CutPlant, thing3);
                        return false;
                    }
                    else
                    {
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
