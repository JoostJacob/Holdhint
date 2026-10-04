on run argv
	set volumeName to item 1 of argv
	set mountPOSIX to item 2 of argv
	tell application "Finder"
		tell disk volumeName
			open
			set current view of container window to icon view
			set toolbar visible of container window to false
			set statusbar visible of container window to false
			set the bounds of container window to {200, 80, 880, 522}
			set viewOptions to the icon view options of container window
			set arrangement of viewOptions to not arranged
			set icon size of viewOptions to 96
			set text size of viewOptions to 12
			delay 0.5
			set background picture of viewOptions to (POSIX file (mountPOSIX & "/.background/background.png") as alias)
			set position of item "Holdhint.app" of container window to {160, 158}
			set position of item "Applications" of container window to {520, 158}
			-- Keep the text file on the disk, outside the window. The picture
			-- already states the steps; a third icon covers the arrow or the warning.
			set position of item "README.txt" of container window to {2400, 2400}
			update without registering applications
			delay 0.5
			close
		end tell
		delay 0.4
		tell disk volumeName
			open
			delay 0.4
			set the bounds of container window to {200, 80, 880, 522}
			set position of item "Holdhint.app" of container window to {160, 158}
			set position of item "Applications" of container window to {520, 158}
			set position of item "README.txt" of container window to {2400, 2400}
			update without registering applications
			delay 0.8
			close
		end tell
	end tell
end run
