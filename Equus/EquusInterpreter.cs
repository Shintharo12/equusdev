using System.Collections.Generic;

using Genelib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

#nullable enable

namespace Equus {
    public class EquusInterpreter : GeneInterpreter {
        public string Name => "Equus";

        void GeneInterpreter.Interpret(EntityBehaviorGenetics genetics) {
            Entity entity = genetics.entity;
            Genome genome = genetics.Genome;
            bool faded = false;
            if (entity.WatchedAttributes.HasAttribute("isfaded")) {
                faded = entity.WatchedAttributes.GetBool("isfaded");
            }
            else {
                faded = entity.World.Rand.Next(3) == 0;
                entity.WatchedAttributes.SetBool("isfaded", faded);
            }
            entity.WatchedAttributes.SetInt("textureIndex", getTextureIndex(genome, faded));
        }

        // Determines which texture to use, ignoring overlays, based on the genes
        private static int getTextureIndex(Genome genome, bool faded) {
            // Shortcuts so we don't forget which number goes with which texture
            int dunmealy = 0; // (and +1 for black base, + 2 for red base)
            int bay = 3; // (and 4 for black base, 5 for red base... that keeps being the case except doublecream)
            int buckskin = 6;
            int baydun = 9;
            int dunskin = 12;
            int baymealy = 15;
            //double cream (shared by cremello, perlino, and smoky cream):
            int doublecream = 18; // same number for black and red bases too - shared texture is not repeated in the list
            int faded_black = 19;
            int faded_grullo = 20;

            // Check the genes to choose a texture
            if (genome.IsHomozygous("cream", "cream")) {
                return doublecream;
            }
            int color = bay;
            if (genome.HasAllele("cream", "cream")) {
                if (genome.HasAllele("dun", "dun")) {
                    color = dunskin;
                }
                else {
                    color = buckskin;
                }
            }
            else if (genome.HasAllele("dun", "dun")) {
                if (genome.HasAllele("mealy", "mealy")) {
                    color = dunmealy;
                }
                else {
                    color = baydun;
                }
            }
            else if (genome.HasAllele("mealy", "mealy")) {
                color = baymealy;
            }

            // Because all textures are arranged in groups of (bay, black, red), except a few special textures at the end,
            // we can use this trick to get the right base color
            if (genome.IsHomozygous("extension", "red")) {
                return color + 2;
            }
            if (genome.IsHomozygous("agouti", "black")) {
                // Faded black only available for leopard patterns for now, because the texture doesn't look good plain
                if (faded && genome.HasAllele("leopard", "leopard")) {
                    if (color == bay) {
                        return faded_black;
                    }
                    if (color == baydun) {
                        return faded_grullo;
                    }
                }
                return color + 1;
            }
            return color;
        }

        // Returns a translated name for the horse's color if leopard and pinto are ignored
        protected string getBaseCoatName(Genome genome) {
            if (genome.IsHomozygous("cream", "cream")) {
                return Lang.Get("genelib:horsecoat-doublecream");
            }
            string baseName = "bay";
            if (genome.IsHomozygous("extension", "red")) {
                baseName = "chestnut";
            }
            else if (genome.IsHomozygous("agouti", "black")) {
                baseName = "black";
            }

            if (genome.HasAllele("dun", "dun")) {
                baseName += "-dun";
            }

            if (genome.HasAllele("cream", "cream") && !baseName.StartsWith("black")) {
                baseName += "-cream";
            }

            if (genome.HasAllele("roan", "roan") && !genome.HasAllele("leopard", "leopard")) {
                if (Lang.HasTranslation("genelib:horsecoat-" + baseName + "-roan")) {
                    return Lang.Get("genelib:horsecoat-" + baseName + "-roan");
                }
                return Lang.Get("genelib:horsecoat-other-roan", Lang.Get("genelib:horsecoat-" + baseName));
            }
            // Minor modifiers which we only list if nothing more interesting is going on
            if (genome.HasAllele("mealy", "mealy") && (baseName == "bay" || baseName == "chestnut")) {
                baseName += "-mealy";
            }

            return Lang.Get("genelib:horsecoat-" + baseName);
        }

        protected string? getPatternName(Genome genome) {
            if (genome.HasAllele("tobiano", "tobiano")) {
                if (genome.HasAllele("leopard", "leopard")) {
                    return "genelib:horsecoat-pintaloosa";
                }
                return "genelib:horsecoat-tobiano";
            }

            if (genome.IsHomozygous("leopard", "leopard")) {
                // Only snowcap is available for this case
                return "genelib:horsecoat-snowcap";
            }
            else if (genome.HasAllele("leopard", "leopard")) {
                // Options range up to nearly full leopard but let's keep it simple
                return "genelib:horsecoat-spottedblanket";
            }
            return null;
        }

        void GeneInterpreter.ListPhenotype(EntityBehaviorGenetics genetics, List<string> descriptions) {
            string baseCoat = getBaseCoatName(genetics.Genome);
            string? pattern = getPatternName(genetics.Genome);

            if (pattern == null) {
                descriptions.Add(Lang.Get("genelib:animalinfo-coatcolor", baseCoat));
            }
            else {
                descriptions.Add(Lang.Get("genelib:animalinfo-basecoat", baseCoat));
                descriptions.Add(Lang.Get("genelib:animalinfo-pattern", Lang.Get(pattern)));
            }
        }
    }
}
