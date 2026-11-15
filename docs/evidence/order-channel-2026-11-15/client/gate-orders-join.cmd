# Order-gate scene built around the client's settled join position (0.60, 34.28, 1.00) - the player
# is NOT moved, because on this build a transform set and the server's copy of the position fight
# and the character snaps back mid-order. Platform gives the nisse a collider; chest B (index 0 by x
# then z: -0.5 before 2.5) is put on the network by the mod's console command; chest A holds coal.
cleanup
hold on
spawn wood_floor -1.40 34.25 -1.00
spawn wood_floor -1.40 34.25 1.00
spawn wood_floor -1.40 34.25 3.00
spawn wood_floor 0.60 34.25 -1.00
spawn wood_floor 0.60 34.25 1.00
spawn wood_floor 0.60 34.25 3.00
spawn wood_floor 2.60 34.25 -1.00
spawn wood_floor 2.60 34.25 1.00
spawn wood_floor 2.60 34.25 3.00
spawn husnisse_bowl -1.60 34.37 1.00 -202584743
spawn piece_chest_wood 2.80 34.37 2.50
spawn piece_chest_wood 2.80 34.37 -0.50
wait 3
console husnisse_rule net piece_chest_wood on 0
husnisse claims
fill husnisse_bowl CookedMeat 3
fill piece_chest_wood Coal 6 1
zdo husnisse_bowl husnisse_bowl_init
zdo piece_chest_wood husnisse_net 0
zdo piece_chest_wood husnisse_net 1
husnisse status
wait 240
