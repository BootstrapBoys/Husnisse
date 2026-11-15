# Order-gate scene on the spawn terrain the dedicated server actually has loaded (the fixture's
# spawn zone). Away from it the server has no terrain collider and the nisse falls through the
# world (seen at y=20 while the ground is 36). Coordinates match the 0.3a UI scene's ground.
cleanup
hold on
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
