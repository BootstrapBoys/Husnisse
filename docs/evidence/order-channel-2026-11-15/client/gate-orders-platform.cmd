# Order-gate scene on the spawn terrain: a ground-level platform so the nisse has a collider under
# it (a character with no ground falls through the server's empty world - seen at y=-4496), the
# bowl owned by the joining client, chest A with coal, chest B to be put on the network from the UI.
cleanup
hold on
spawn wood_floor -7.00 34.07 1.00
spawn wood_floor -5.00 34.07 1.00
spawn wood_floor -3.00 34.07 1.00
spawn wood_floor -1.00 34.07 1.00
spawn wood_floor 1.00 34.07 1.00
spawn wood_floor -7.00 34.07 -2.00
spawn wood_floor -5.00 34.07 -2.00
spawn wood_floor -3.00 34.07 -2.00
spawn wood_floor -1.00 34.07 -2.00
spawn wood_floor 1.00 34.07 -2.00
spawn husnisse_bowl -4.00 34.17 -2.00 -202584743
spawn piece_chest_wood -1.00 34.17 1.00
spawn piece_chest_wood -7.00 34.17 1.00
wait 3
husnisse claims
fill husnisse_bowl CookedMeat 3
fill piece_chest_wood Coal 6 1
zdo husnisse_bowl husnisse_bowl_init
zdo husnisse_bowl husnisse_bowl_satiety
husnisse status
wait 240
