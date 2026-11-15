# Does zdoset work on a freshly spawned chest in the same scenario?
cleanup
hold on
spawn piece_chest_wood 0 ground 0
wait 2
zdoset piece_chest_wood husnisse_net bool true 0
zdo piece_chest_wood husnisse_net
hold off
