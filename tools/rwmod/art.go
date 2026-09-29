package main

// art re-renders About/ModIcon.png and About/Preview.png from the SVG sources
// in art/. The PNGs are committed, so this only needs running after editing an
// SVG. Needs rsvg-convert (librsvg) on PATH.

import (
	"errors"
	"flag"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
)

type artTarget struct {
	src    string
	out    string
	width  int
	height int
}

// RimWorld draws ModIcon.png small (roughly 32px in the mod list), so it is
// rendered at 64 for a bit of headroom. Preview.png is the 16:9 image shown in
// the mod details pane and on the Workshop.
var artTargets = []artTarget{
	{"art/icon.svg", "About/ModIcon.png", 64, 64},
	{"art/preview.svg", "About/Preview.png", 640, 360},
}

func art(args []string) error {
	fs := flag.NewFlagSet("art", flag.ExitOnError)
	fs.Parse(args)

	root, err := repoRoot()
	if err != nil {
		return err
	}
	rsvg, err := exec.LookPath("rsvg-convert")
	if err != nil {
		return errors.New("rsvg-convert not found on PATH (install librsvg); " +
			"the rendered PNGs are committed, so this is only needed after editing art/*.svg")
	}

	for _, t := range artTargets {
		src := filepath.Join(root, t.src)
		out := filepath.Join(root, t.out)
		if _, err := os.Stat(src); err != nil {
			return fmt.Errorf("missing source %s: %w", t.src, err)
		}

		cmd := exec.Command(rsvg,
			"-w", fmt.Sprint(t.width), "-h", fmt.Sprint(t.height), src, "-o", out)
		cmd.Stdout, cmd.Stderr = os.Stdout, os.Stderr
		if err := cmd.Run(); err != nil {
			return fmt.Errorf("rendering %s: %w", t.src, err)
		}
		fmt.Printf("%s -> %s (%dx%d)\n", t.src, t.out, t.width, t.height)
	}
	return nil
}
