using Genelib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Equus {
    public class EquusInterpreter : GeneInterpreter {
        public string Name => "Equus";

        void GeneInterpreter.Interpret(EntityBehaviorGenetics genetics) {
            Entity entity = genetics.entity;
            Genome genome = genetics.Genome;
            entity.WatchedAttributes.SetInt("textureIndex", getTextureIndex(genome));
        }

        // Determines which texture to use, ignoring roan, based on the genes
        private static int getSolidBase(Genome genome) {
            // Shortcuts so we don't forget which number goes with which texture
            int dunmealy = 0; // (and +1 for black base, + 2 for red base)
            int bay = 3; // (and 4 for black base, 5 for red base... that keeps being the case except doublecream)
            int buckskin = 6;
            int baydun = 9;
            int dunskin = 12;
            int baymealy = 15;
            int bayleopard = 18;
            int baydunleopard = 21;
            //double cream (shared by cremello, perlino, and smoky cream):
            int doublecream = 24; // same number for black and red basees too - shared texture is not repeated in the list

            // Check the genes to choose a texture
            if (genome.IsHomozygous("cream", "cream")) {
                return doublecream;
            }
            int color = bay;
            // Give leopard the highest priority - if we don't have a texture for this exact horse, we pick the closest leopard texture
            if (genome.HasAllele("leopard", "leopard")) {
                if (genome.HasAllele("dun", "dun")) {
                    color = baydunleopard;
                }
                else {
                    color = bayleopard;
                }
            }
            else if (genome.HasAllele("cream", "cream")) {
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

            // Because all textures are arranged in groups of (bay, black, red), except doublecream which 
            // was already handled and is last so not in the way, we can use this trick to get the right base color
            if (genome.IsHomozygous("extension", "red")) {
                return color + 2;
            }
            if (genome.IsHomozygous("agouti", "black")) {
                return color + 1;
            }
            return color;
        }

        private static int getTextureIndex(Genome genome)
        {
            int texture = getSolidBase(genome);
            if (genome.HasAllele("roan", "roan")&& genome.HasAllele("tobiano", "tobiano"))
            {
                return texture + 75; // 50 non-tobiano textures, then 25 tobiano textures, then 25 roan+tobiano textures
            }
            if (genome.HasAllele("tobiano", "tobiano"))
            {
                return texture + 50; // 50 non-tobiano textures, then 25 tobiano textures
            }
            if (genome.HasAllele("roan", "roan"))
            {
                return texture + 25; // 25 non-roan textures, then 25 roan textures
            }
            return texture;
        }
    }
}
