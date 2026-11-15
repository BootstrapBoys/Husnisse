hold on
cleanup
hold on
spawn wood_floor 58.08 41.85 58.19
spawn wood_floor 58.08 41.85 60.19
spawn wood_floor 58.08 41.85 62.19
spawn wood_floor 60.08 41.85 58.19
spawn wood_floor 60.08 41.85 60.19
spawn wood_floor 60.08 41.85 62.19
spawn wood_floor 62.08 41.85 58.19
spawn wood_floor 62.08 41.85 60.19
spawn wood_floor 62.08 41.85 62.19
spawn piece_groundtorch_green 58.8 41.95 61.8
spawn piece_groundtorch_green 61.4 41.95 61.8
spawn husnisse_bowl 58.08 41.95 59.19 -202584743
spawn piece_chest_wood 62.08 41.95 59.19
spawn piece_chest_wood 62.08 41.95 62.19
wait 3
husnisse claims
fill husnisse_bowl CookedMeat 2
zdoset husnisse_bowl husnisse_bowl_init bool true
zdoset husnisse_bowl husnisse_bowl_feeding bool true
zdoset husnisse_bowl husnisse_bowl_satiety float 100
zdoset husnisse_bowl husnisse_bowl_mood float 77
wait 2
zdo husnisse_bowl husnisse_bowl_feeding
zdo husnisse_bowl husnisse_bowl_satiety
husnisse status
