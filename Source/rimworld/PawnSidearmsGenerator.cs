﻿using RimWorld;
using SimpleSidearms.utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace SimpleSidearms.rimworld
{
    public static class PawnSidearmsGenerator
    {

        public static void listWeapons(string prefix, IEnumerable<ThingStuffPair> weapons)
        {
            string output = prefix;
            foreach(var pair in weapons)
            {
                output += pair.thing.defName + ":" + pair.stuff + " (" + pair.Price + ") ";
            }
            Log.Message(output);
        }

        //reworked to use my own code instead of generator code nicked from vanilla Rimworld
        public static bool TryGenerateSidearmFor(Pawn pawn, float chance, float budgetMultiplier, PawnGenerationRequest request)
        {
            if (
                pawn is null ||
                SimpleSidearms.saveData is null || //not yet in game
                chance < 0.01f ||
                pawn.kindDef.weaponTags == null || pawn.kindDef.weaponTags.Count == 0 ||
                !pawn.RaceProps.ToolUser ||
                !pawn.RaceProps.Humanlike ||
                !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) ||
                (pawn.story != null && pawn.story.DisabledWorkTagsBackstoryAndTraits.OverlapsWithOnAnyWorkType(WorkTags.Violent))
                )
            {
                return false;
            }
            else
            {
                //bool primarySingleUse = pawn.equipment.Primary.GetComp<CompEquippable>().PrimaryVerb is Verb_ShootOneUse;

                ThingStuffPair primaryBase = new ThingStuffPair(pawn.equipment.Primary.def, pawn.equipment.Primary.Stuff);

                var sidearmChanceRoll = Rand.ValueSeeded(pawn.thingIDNumber ^ 28554824);
                if (sidearmChanceRoll >= chance)
                    return false; //rolled no sidearm

                IEnumerable<ThingStuffPair> validSidearms = GettersFilters.getValidSidearms();

                IEnumerable<string> weaponTags = generateWeaponTags(pawn.kindDef.weaponTags);

                validSidearms = validSidearms.Where(t =>
                {
                    foreach (string tag in t.thing.weaponTags)
                    {
                        if (t == null || t.thing == null || t.thing.weaponTags == null)
                            continue;
                        if (weaponTags.Contains(tag))
                            return true;
                    }
                    return false;
                });

                    //filter out nonsensical material weapons
                validSidearms = validSidearms.Where(w => w.stuff == null || (w.stuff != ThingDefOf.Gold && w.stuff != ThingDefOf.Silver && w.stuff != ThingDefOf.Uranium));
                    //filter out weapons the pawn cant carry
                validSidearms = validSidearms.Where(w => StatCalculator.canCarrySidearmType(w, pawn, out _));

                bool onlyMelee = (pawn.story != null && pawn.story.traits.HasTrait(TraitDefOf.Brawler));
                bool onlyRanged = (pawn.story != null && pawn.story.traits.HasTrait(TraitDef.Named("Wimp"))); //wimp has no defOf

                if (onlyMelee)
                    validSidearms = validSidearms.Where(w => w.thing.IsMeleeWeapon);
                if (onlyRanged)
                    validSidearms = validSidearms.Where(w => w.thing.IsRangedWeapon);

                //listWeapons("budget " + pawn.kindDef.weaponMoney.max* budgetMultiplier + " to " + pawn.kindDef.weaponMoney.min* budgetMultiplier + ", main "+ pawn.equipment.Primary.MarketValue +" selecting from:", validSidearms);

                    //use the value of primary to limit budget maximum (but of the base to match pre-degradation value) to prevent sidearms being better than the primary weapon
                float budget = Math.Min(pawn.kindDef.weaponMoney.RandomInRange, primaryBase.Price) * budgetMultiplier;
                validSidearms = validSidearms.Where(t => t.Price <= budget);

                //listWeapons("post select:", validSidearms);

                if (validSidearms.Count() == 0)
                    return false;

                ThingStuffPair rolledWeaponThingStuff;

                validSidearms.TryRandomElementByWeight(t => {return t.Commonality * t.Price;}, out rolledWeaponThingStuff);

                ThingWithComps rolledWeaponFinal = (ThingWithComps)ThingMaker.MakeThing(rolledWeaponThingStuff.thing, rolledWeaponThingStuff.stuff);
                PawnGenerator.PostProcessGeneratedGear(rolledWeaponFinal, pawn);

                float num = (request.BiocodeWeaponChance > 0f) ? request.BiocodeWeaponChance : pawn.kindDef.biocodeWeaponChance;
                if (Rand.Value < num)
                {
                    CompBiocodableWeapon compBiocodableWeapon = rolledWeaponFinal.TryGetComp<CompBiocodableWeapon>();
                    if (compBiocodableWeapon != null)
                    {
                        compBiocodableWeapon.CodeFor(pawn);
                    }
                }

                bool success = pawn.inventory.innerContainer.TryAdd(rolledWeaponFinal);
                if (success)
                    GoldfishModule.GetGoldfishForPawn(pawn).InformOfAddedSidearm(rolledWeaponFinal);
                else
                    Log.Warning("Failed to add generated sidearm to inventory");

                return true;
            }
        }

        public static IEnumerable<string> generateWeaponTags(IEnumerable<string> sourceTags)
        {
            HashSet<string> resultTags = new HashSet<string>();

            IEnumerable<SidearmWeaponTagMapDef> mapDefs = DefDatabase<SidearmWeaponTagMapDef>.AllDefs;

            bool foundAnyMap = false;
            foreach (var mapDef in mapDefs.Where(d => sourceTags.Contains(d.sourceTag)))
            {
                foundAnyMap = true;
                resultTags.AddRange(mapDef.resultTags);
            }

            if (!foundAnyMap) 
            {
                Log.Message("SimpleSidearms warning: no weaponTag sidearm map found for following source tags: (" + String.Join(",", sourceTags)+")");
                //as a fallback, use the source tags 
                resultTags.AddRange(sourceTags);
            }
            return resultTags;
        }
    }
}