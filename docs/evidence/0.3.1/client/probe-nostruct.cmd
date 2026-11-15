# Survival probe with WearNTear removed: a bowl + chest must still be there 95 s after spawn.
cleanup
hold on
spawn piece_chest_wood -6 34.2 0
spawn husnisse_bowl -6 34.2 2 -202584743
nostruct
wait 3
zdo husnisse_bowl husnisse_bowl_init
husnisse claims
wait 95
husnisse claims
husnisse containers
