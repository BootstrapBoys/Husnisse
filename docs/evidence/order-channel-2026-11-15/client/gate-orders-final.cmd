# Order-gate scene, final shape: platform on the spawn terrain (the nisse needs a collider under it),
# bowl owned by the joining client, chest A with coal, and chest B put on the storage network by the
# mod's own console command (the client cannot open a server-spawned chest's window on this build -
# Container.Interact returns true but InventoryGui never shows, the same wall the 0.3a sweep hit).
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
console husnisse_rule net piece_chest_wood on 0
console husnisse_rule list
husnisse claims
fill husnisse_bowl CookedMeat 3
fill piece_chest_wood Coal 6 1
zdo husnisse_bowl husnisse_bowl_init
zdo piece_chest_wood husnisse_net 0
zdo piece_chest_wood husnisse_net 1
husnisse status
wait 240
