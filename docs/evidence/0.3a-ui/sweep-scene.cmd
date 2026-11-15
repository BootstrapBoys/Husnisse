# 0.3a UI sweep scene: bowl (creator -202584743) + 20 chests, held against ZNetScene culling.
cleanup
hold on
spawn wood_floor -6 34.07 -6
spawn wood_floor -6 34.07 -4
spawn wood_floor -6 34.07 -2
spawn wood_floor -6 34.07 0
spawn wood_floor -6 34.07 2
spawn wood_floor -6 34.07 4
spawn wood_floor -4 34.07 -6
spawn wood_floor -4 34.07 -4
spawn wood_floor -4 34.07 -2
spawn wood_floor -4 34.07 0
spawn wood_floor -4 34.07 2
spawn wood_floor -4 34.07 4
spawn wood_floor -2 34.07 -6
spawn wood_floor -2 34.07 -4
spawn wood_floor -2 34.07 -2
spawn wood_floor -2 34.07 0
spawn wood_floor -2 34.07 2
spawn wood_floor -2 34.07 4
spawn wood_floor 0 34.07 -6
spawn wood_floor 0 34.07 -4
spawn wood_floor 0 34.07 -2
spawn wood_floor 0 34.07 0
spawn wood_floor 0 34.07 2
spawn wood_floor 0 34.07 4
spawn wood_floor 2 34.07 -6
spawn wood_floor 2 34.07 -4
spawn wood_floor 2 34.07 -2
spawn wood_floor 2 34.07 0
spawn wood_floor 2 34.07 2
spawn wood_floor 2 34.07 4
spawn wood_floor 4 34.07 -6
spawn wood_floor 4 34.07 -4
spawn wood_floor 4 34.07 -2
spawn wood_floor 4 34.07 0
spawn wood_floor 4 34.07 2
spawn wood_floor 4 34.07 4
spawn wood_floor 6 34.07 -6
spawn wood_floor 6 34.07 -4
spawn wood_floor 6 34.07 -2
spawn wood_floor 6 34.07 0
spawn wood_floor 6 34.07 2
spawn wood_floor 6 34.07 4
spawn husnisse_bowl -4 34.17 -4 -202584743
spawn piece_chest_wood -2 34.17 -2
spawn piece_chest_wood -2 34.17 0
spawn piece_chest_wood -2 34.17 2
spawn piece_chest_wood -2 34.17 4
spawn piece_chest_wood 0 34.17 -2
spawn piece_chest_wood 0 34.17 0
spawn piece_chest_wood 0 34.17 2
spawn piece_chest_wood 0 34.17 4
spawn piece_chest_wood 2 34.17 -2
spawn piece_chest_wood 2 34.17 0
spawn piece_chest_wood 2 34.17 2
spawn piece_chest_wood 2 34.17 4
spawn piece_chest_wood 4 34.17 -2
spawn piece_chest_wood 4 34.17 0
spawn piece_chest_wood 4 34.17 2
spawn piece_chest_wood 4 34.17 4
spawn piece_chest_wood 6 34.17 -2
spawn piece_chest_wood 6 34.17 0
spawn piece_chest_wood 6 34.17 2
spawn piece_chest_wood 6 34.17 4
wait 5
zdo husnisse_bowl husnisse_bowl_init
husnisse claims
expect_claim owner=-202584743
husnisse status
expect_ge containers=(\d+) 20
expect_ge ruled=(\d+) 0
