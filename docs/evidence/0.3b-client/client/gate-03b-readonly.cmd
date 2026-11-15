# 0.3b client gate: another player's claim (index 0) + no-claim chest (index 1).
cleanup
hold on
spawn wood_floor 2 46.07 6
spawn wood_floor 2 46.07 8
spawn wood_floor 2 46.07 10
spawn wood_floor 4 46.07 6
spawn wood_floor 4 46.07 8
spawn wood_floor 4 46.07 10
spawn wood_floor 6 46.07 6
spawn wood_floor 6 46.07 8
spawn wood_floor 6 46.07 10
spawn wood_floor 8 46.07 6
spawn wood_floor 8 46.07 8
spawn wood_floor 8 46.07 10
spawn wood_floor 30 46.07 4
spawn wood_floor 30 46.07 6
spawn wood_floor 30 46.07 8
spawn wood_floor 32 46.07 4
spawn wood_floor 32 46.07 6
spawn wood_floor 32 46.07 8
spawn wood_floor 34 46.07 4
spawn wood_floor 34 46.07 6
spawn wood_floor 34 46.07 8
spawn husnisse_bowl 4 46.17 8 424242
spawn piece_chest_wood 6 46.17 8
spawn piece_chest_wood 32 46.17 6
wait 6
zdo husnisse_bowl husnisse_bowl_init
husnisse claims
expect_claim owner=424242
husnisse status
expect_ge containers=(\d+) 1
