# 0.4 client gate scene, generated at (-126.81, 34.12, -6.86).
# A floor for support, one bowl owned by the client, one chest to hold the claim's stock.
cleanup
hold on
spawn wood_floor -128.81 34.22 -8.86
spawn wood_floor -128.81 34.22 -7.86
spawn wood_floor -128.81 34.22 -6.86
spawn wood_floor -128.81 34.22 -5.86
spawn wood_floor -128.81 34.22 -4.86
spawn wood_floor -127.81 34.22 -8.86
spawn wood_floor -127.81 34.22 -7.86
spawn wood_floor -127.81 34.22 -6.86
spawn wood_floor -127.81 34.22 -5.86
spawn wood_floor -127.81 34.22 -4.86
spawn wood_floor -126.81 34.22 -8.86
spawn wood_floor -126.81 34.22 -7.86
spawn wood_floor -126.81 34.22 -6.86
spawn wood_floor -126.81 34.22 -5.86
spawn wood_floor -126.81 34.22 -4.86
spawn wood_floor -125.81 34.22 -8.86
spawn wood_floor -125.81 34.22 -7.86
spawn wood_floor -125.81 34.22 -6.86
spawn wood_floor -125.81 34.22 -5.86
spawn wood_floor -125.81 34.22 -4.86
spawn wood_floor -124.81 34.22 -8.86
spawn wood_floor -124.81 34.22 -7.86
spawn wood_floor -124.81 34.22 -6.86
spawn wood_floor -124.81 34.22 -5.86
spawn wood_floor -124.81 34.22 -4.86
spawn husnisse_bowl -125.81 34.32 -6.86 -202584743
spawn piece_chest_wood -124.81 34.32 -6.86
wait 6
nostruct
zdo husnisse_bowl husnisse_bowl_init
husnisse claims
expect_claim owner=-202584743
fill piece_chest_wood Wood 30
container piece_chest_wood 0
