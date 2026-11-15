# 0.3b client gate scene (compact, p8-p13), generated at (-131, 78.01, 189).
# bowl creator -202584743 (the joining player); role -> index map in gate-03b-indices.env.
cleanup
hold on
spawn wood_floor -136 78.01 170
spawn wood_floor -136 78.01 172
spawn wood_floor -136 78.01 174
spawn wood_floor -136 78.01 176
spawn wood_floor -136 78.01 178
spawn wood_floor -136 78.01 180
spawn wood_floor -136 78.01 182
spawn wood_floor -136 78.01 184
spawn wood_floor -136 78.01 186
spawn wood_floor -136 78.01 188
spawn wood_floor -136 78.01 190
spawn wood_floor -136 78.01 192
spawn wood_floor -136 78.01 194
spawn wood_floor -134 78.01 170
spawn wood_floor -134 78.01 172
spawn wood_floor -134 78.01 174
spawn wood_floor -134 78.01 176
spawn wood_floor -134 78.01 178
spawn wood_floor -134 78.01 180
spawn wood_floor -134 78.01 182
spawn wood_floor -134 78.01 184
spawn wood_floor -134 78.01 186
spawn wood_floor -134 78.01 188
spawn wood_floor -134 78.01 190
spawn wood_floor -134 78.01 192
spawn wood_floor -134 78.01 194
spawn wood_floor -132 78.01 170
spawn wood_floor -132 78.01 172
spawn wood_floor -132 78.01 174
spawn wood_floor -132 78.01 176
spawn wood_floor -132 78.01 178
spawn wood_floor -132 78.01 180
spawn wood_floor -132 78.01 182
spawn wood_floor -132 78.01 184
spawn wood_floor -132 78.01 186
spawn wood_floor -132 78.01 188
spawn wood_floor -132 78.01 190
spawn wood_floor -132 78.01 192
spawn wood_floor -132 78.01 194
spawn wood_floor -130 78.01 170
spawn wood_floor -130 78.01 172
spawn wood_floor -130 78.01 174
spawn wood_floor -130 78.01 176
spawn wood_floor -130 78.01 178
spawn wood_floor -130 78.01 180
spawn wood_floor -130 78.01 182
spawn wood_floor -130 78.01 184
spawn wood_floor -130 78.01 186
spawn wood_floor -130 78.01 188
spawn wood_floor -130 78.01 190
spawn wood_floor -130 78.01 192
spawn wood_floor -130 78.01 194
spawn wood_floor -128 78.01 170
spawn wood_floor -128 78.01 172
spawn wood_floor -128 78.01 174
spawn wood_floor -128 78.01 176
spawn wood_floor -128 78.01 178
spawn wood_floor -128 78.01 180
spawn wood_floor -128 78.01 182
spawn wood_floor -128 78.01 184
spawn wood_floor -128 78.01 186
spawn wood_floor -128 78.01 188
spawn wood_floor -128 78.01 190
spawn wood_floor -128 78.01 192
spawn wood_floor -128 78.01 194
spawn wood_floor -126 78.01 170
spawn wood_floor -126 78.01 172
spawn wood_floor -126 78.01 174
spawn wood_floor -126 78.01 176
spawn wood_floor -126 78.01 178
spawn wood_floor -126 78.01 180
spawn wood_floor -126 78.01 182
spawn wood_floor -126 78.01 184
spawn wood_floor -126 78.01 186
spawn wood_floor -126 78.01 188
spawn wood_floor -126 78.01 190
spawn wood_floor -126 78.01 192
spawn wood_floor -126 78.01 194
spawn husnisse_bowl -131 78.11 189 -202584743
spawn piece_chest_wood -129 78.11 189
spawn piece_chest_wood -127 78.11 189
spawn piece_chest_wood -129 78.11 191
spawn piece_chest_wood -133 78.11 175
spawn piece_chest_wood -129 78.11 175
spawn piece_chest_wood -131 78.11 173
spawn piece_chest_wood -133 78.11 177
spawn piece_chest_wood -129 78.11 177
spawn piece_chest_wood -131 78.11 179
wait 6
zdo husnisse_bowl husnisse_bowl_init
husnisse claims
expect_claim owner=-202584743
husnisse status
expect_ge containers=(\d+) 8
console husnisse_rule net piece_chest_wood on 8
console husnisse_rule net piece_chest_wood on 7
console husnisse_rule net piece_chest_wood on 0
console husnisse_rule net piece_chest_wood on 4
console husnisse_rule net piece_chest_wood on 2
console husnisse_rule net piece_chest_wood on 1
console husnisse_rule net piece_chest_wood on 5
console husnisse_rule set piece_chest_wood cat:metals 0
console husnisse_rule set piece_chest_wood cat:wood 4
console husnisse_rule set piece_chest_wood overflow 2
console husnisse_rule set piece_chest_wood deposit 1
console husnisse_rule set piece_chest_wood cat:wood 6
wait 2
husnisse status
