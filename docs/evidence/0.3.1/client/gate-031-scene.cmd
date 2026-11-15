# 0.3.1 client gate scene, generated at (-6, 34.10, 1); bowl creator -202584743.
# role -> index map in gate-031-indices.env.
cleanup
hold on
spawn wood_floor -10 34.10 -3
spawn wood_floor -10 34.10 -1
spawn wood_floor -10 34.10 1
spawn wood_floor -10 34.10 3
spawn wood_floor -10 34.10 5
spawn wood_floor -8 34.10 -3
spawn wood_floor -8 34.10 -1
spawn wood_floor -8 34.10 1
spawn wood_floor -8 34.10 3
spawn wood_floor -8 34.10 5
spawn wood_floor -6 34.10 -3
spawn wood_floor -6 34.10 -1
spawn wood_floor -6 34.10 1
spawn wood_floor -6 34.10 3
spawn wood_floor -6 34.10 5
spawn wood_floor -4 34.10 -3
spawn wood_floor -4 34.10 -1
spawn wood_floor -4 34.10 1
spawn wood_floor -4 34.10 3
spawn wood_floor -4 34.10 5
spawn wood_floor -2 34.10 -3
spawn wood_floor -2 34.10 -1
spawn wood_floor -2 34.10 1
spawn wood_floor -2 34.10 3
spawn wood_floor -2 34.10 5
spawn husnisse_bowl -6 34.20 1 -202584743
spawn piece_chest_wood -4 34.20 1
spawn piece_chest_wood -8 34.20 1
spawn piece_chest_wood -6 34.20 3
spawn piece_chest_wood -6 34.20 -1
wait 6
nostruct
zdo husnisse_bowl husnisse_bowl_init
husnisse claims
expect_claim owner=-202584743
husnisse status
expect_ge containers=(\d+) 4
