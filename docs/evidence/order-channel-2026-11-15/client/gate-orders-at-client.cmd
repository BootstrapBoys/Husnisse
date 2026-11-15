# Order-gate scene at the client's own settled position (the character ZDO puts the player at
# -30.3, 35.4, 4.7 and the client snaps back there, so the scene comes to the player instead of
# moving the player to the scene). A ground-level platform gives the nisse a collider; chest B is
# put on the network by the mod's console command; chest A holds the coal for the Tidy.
cleanup
hold on
spawn wood_floor -32.30 35.40 5.70
spawn wood_floor -32.30 35.40 3.70
spawn wood_floor -32.30 35.40 1.70
spawn wood_floor -30.30 35.40 5.70
spawn wood_floor -30.30 35.40 3.70
spawn wood_floor -30.30 35.40 1.70
spawn wood_floor -28.30 35.40 5.70
spawn wood_floor -28.30 35.40 3.70
spawn wood_floor -28.30 35.40 1.70
spawn husnisse_bowl -32.50 35.50 3.00 -202584743
spawn piece_chest_wood -28.50 35.50 6.00
spawn piece_chest_wood -28.50 35.50 3.00
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
