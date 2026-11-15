# Order-gate scene, ground level (the floating deck from the client-gate scene decays and takes the
# bowl with it within a minute - WearNTear support). Y is the fixture ground the client stands on.
cleanup
hold on
spawn husnisse_bowl -32.17 36.45 -0.97 -202584743
spawn piece_chest_wood -28.17 36.45 -0.97
spawn piece_chest_wood -28.17 36.45 2.03
wait 3
husnisse claims
fill husnisse_bowl CookedMeat 3
fill piece_chest_wood Coal 6 0
zdo husnisse_bowl husnisse_bowl_init
zdo husnisse_bowl husnisse_bowl_satiety
husnisse status
wait 240
