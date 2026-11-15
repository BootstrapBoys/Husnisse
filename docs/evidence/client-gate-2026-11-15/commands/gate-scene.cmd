# Gate scene (rebuilt after the harness reboot): 3x3 deck 2 m above the client at
# (-30.17, 36.46, 0.03), bowl owned by the joining player, chest A holds coal,
# chest B is the routing target.
cleanup
hold on
spawn wood_floor -32.17 38.46 -1.97
spawn wood_floor -32.17 38.46 -0.05
spawn wood_floor -32.17 38.46 2.03
spawn wood_floor -30.17 38.46 -1.97
spawn wood_floor -30.17 38.46 -0.05
spawn wood_floor -30.17 38.46 2.03
spawn wood_floor -28.17 38.46 -1.97
spawn wood_floor -28.17 38.46 -0.05
spawn wood_floor -28.17 38.46 2.03
spawn piece_groundtorch_green -31.4 38.56 1.7
spawn piece_groundtorch_green -28.8 38.56 1.7
spawn husnisse_bowl -32.17 38.56 -0.97 -202584743
spawn piece_chest_wood -28.17 38.56 -0.97
spawn piece_chest_wood -28.17 38.56 2.03
wait 3
husnisse claims
fill husnisse_bowl CookedMeat 3
fill piece_chest_wood Coal 6 0
zdo husnisse_bowl husnisse_bowl_init
zdo husnisse_bowl husnisse_bowl_satiety
zdo piece_chest_wood items 0
zdo piece_chest_wood items 1
husnisse status
