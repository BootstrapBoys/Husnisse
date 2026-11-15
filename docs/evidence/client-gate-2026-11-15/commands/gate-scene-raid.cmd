# Gate scene for the raid check: deck at (-60, 38.78, 20), bowl owned by the joining
# player, chest A holds coal, chest B is the routing target.
cleanup
hold on
spawn wood_floor -62.17 38.78 17.97
spawn wood_floor -62.17 38.78 19.89
spawn wood_floor -62.17 38.78 21.97
spawn wood_floor -60.17 38.78 17.97
spawn wood_floor -60.17 38.78 19.89
spawn wood_floor -60.17 38.78 21.97
spawn wood_floor -58.17 38.78 17.97
spawn wood_floor -58.17 38.78 19.89
spawn wood_floor -58.17 38.78 21.97
spawn piece_groundtorch_green -61.4 38.88 21.6
spawn piece_groundtorch_green -58.8 38.88 21.6
spawn husnisse_bowl -62.17 38.88 19.03 -202584743
spawn piece_chest_wood -58.17 38.88 19.03
spawn piece_chest_wood -58.17 38.88 22.03
wait 3
husnisse claims
fill husnisse_bowl CookedMeat 3
fill piece_chest_wood Coal 6 0
zdo husnisse_bowl husnisse_bowl_init
zdoset husnisse_bowl husnisse_bowl_feeding bool true
zdo husnisse_bowl husnisse_bowl_satiety
husnisse status
