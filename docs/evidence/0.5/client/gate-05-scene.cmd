# 0.5 client gate scene, generated at (1.56, 33.57, 0.52).
# A floor for support, a workbench for the craft, the bowl, and a chest holding the claim's stock.
cleanup
hold on
spawn wood_floor -0.44 33.67 -1.48
spawn wood_floor -0.44 33.67 -0.48
spawn wood_floor -0.44 33.67 0.52
spawn wood_floor -0.44 33.67 1.52
spawn wood_floor -0.44 33.67 2.52
spawn wood_floor 0.56 33.67 -1.48
spawn wood_floor 0.56 33.67 -0.48
spawn wood_floor 0.56 33.67 0.52
spawn wood_floor 0.56 33.67 1.52
spawn wood_floor 0.56 33.67 2.52
spawn wood_floor 1.56 33.67 -1.48
spawn wood_floor 1.56 33.67 -0.48
spawn wood_floor 1.56 33.67 0.52
spawn wood_floor 1.56 33.67 1.52
spawn wood_floor 1.56 33.67 2.52
spawn wood_floor 2.56 33.67 -1.48
spawn wood_floor 2.56 33.67 -0.48
spawn wood_floor 2.56 33.67 0.52
spawn wood_floor 2.56 33.67 1.52
spawn wood_floor 2.56 33.67 2.52
spawn wood_floor 3.56 33.67 -1.48
spawn wood_floor 3.56 33.67 -0.48
spawn wood_floor 3.56 33.67 0.52
spawn wood_floor 3.56 33.67 1.52
spawn wood_floor 3.56 33.67 2.52
spawn piece_workbench 3.56 33.77 -0.48 -202584743
spawn husnisse_bowl 0.06 33.77 0.52 -202584743
spawn piece_chest_wood 1.56 33.77 3.02
wait 8
nostruct
zdo husnisse_bowl husnisse_bowl_init
husnisse claims
expect_claim owner=-202584743
husnisse containers
expect_ge husnisse containers found=(\d+) 1
