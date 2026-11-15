# Survival probe: build a bowl + chest inside ONE scenario and watch them for 70 s without any new
# scenario loading. If the claim survives the wait, the earlier loss came from a scenario reset.
cleanup
hold on
spawn husnisse_bowl -6 34.2 0 -202584743
spawn piece_chest_wood -4 34.2 0
wait 3
zdo husnisse_bowl husnisse_bowl_init
husnisse claims
wait 70
husnisse claims
husnisse containers
