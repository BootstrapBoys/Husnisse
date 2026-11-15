# Support probe: one chest on the terrain, one floating 4 m up. If the floating one vanishes and the
# grounded one survives, WearNTear collapse is what ate the first 0.3.1 scene, not the mod.
cleanup
hold on
spawn piece_chest_wood -6 ground 0
spawn piece_chest_wood -4 38 0
wait 3
husnisse containers
