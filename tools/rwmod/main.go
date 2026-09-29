// rwmod is the dev tool for this mod.
//
//	go run ./tools/rwmod build   compile Assemblies/CoupleBeds.dll
//	go run ./tools/rwmod link    symlink this repo into RimWorld's Mods folder
//	go run ./tools/rwmod log     show CoupleBeds errors from the game's Player.log
//
// Environment:
//
//	RIMWORLD_DIR  game folder (default ~/Games/RimWorld/game)
package main

import (
	"bufio"
	"errors"
	"flag"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
)

const modFolderName = "CoupleBeds"

func main() {
	if len(os.Args) < 2 {
		usage()
	}
	var err error
	switch os.Args[1] {
	case "build":
		err = build(os.Args[2:])
	case "link":
		err = link(os.Args[2:])
	case "log":
		err = showLog(os.Args[2:])
	default:
		usage()
	}
	if err != nil {
		fmt.Fprintln(os.Stderr, "error:", err)
		os.Exit(1)
	}
}

func usage() {
	fmt.Fprintln(os.Stderr, "usage: go run ./tools/rwmod <build|link|log> [flags]")
	os.Exit(2)
}

func home() string {
	h, err := os.UserHomeDir()
	if err != nil {
		panic(err)
	}
	return h
}

func gameDir() string {
	if d := os.Getenv("RIMWORLD_DIR"); d != "" {
		return d
	}
	return filepath.Join(home(), "Games", "RimWorld", "game")
}

// repoRoot walks up from the working directory to the folder holding About/About.xml.
func repoRoot() (string, error) {
	dir, err := os.Getwd()
	if err != nil {
		return "", err
	}
	for {
		if _, err := os.Stat(filepath.Join(dir, "About", "About.xml")); err == nil {
			return dir, nil
		}
		parent := filepath.Dir(dir)
		if parent == dir {
			return "", errors.New("not inside the mod repo (no About/About.xml found)")
		}
		dir = parent
	}
}

func findDotnet() (string, error) {
	if p, err := exec.LookPath("dotnet"); err == nil {
		return p, nil
	}
	p := filepath.Join(home(), ".dotnet", "dotnet")
	if _, err := os.Stat(p); err == nil {
		return p, nil
	}
	return "", errors.New("dotnet SDK not found on PATH or in ~/.dotnet")
}

func build(args []string) error {
	fs := flag.NewFlagSet("build", flag.ExitOnError)
	config := fs.String("c", "Release", "build configuration")
	fs.Parse(args)

	root, err := repoRoot()
	if err != nil {
		return err
	}
	managed := filepath.Join(gameDir(), "RimWorldLinux_Data", "Managed")
	if _, err := os.Stat(filepath.Join(managed, "Assembly-CSharp.dll")); err != nil {
		return fmt.Errorf("RimWorld DLLs not found in %s (set RIMWORLD_DIR)", managed)
	}
	dotnet, err := findDotnet()
	if err != nil {
		return err
	}

	src := filepath.Join(root, "Source")
	cmd := exec.Command(dotnet, "build", "-c", *config, "-nologo", "-v", "q",
		"-p:RimWorldManaged="+managed)
	cmd.Dir = src
	cmd.Stdout, cmd.Stderr = os.Stdout, os.Stderr
	if err := cmd.Run(); err != nil {
		return fmt.Errorf("dotnet build failed: %w", err)
	}
	os.RemoveAll(filepath.Join(src, "obj"))
	fmt.Println("OK:", filepath.Join(root, "Assemblies", modFolderName+".dll"))
	return nil
}

func link(args []string) error {
	fs := flag.NewFlagSet("link", flag.ExitOnError)
	fs.Parse(args)

	root, err := repoRoot()
	if err != nil {
		return err
	}
	target := filepath.Join(gameDir(), "Mods", modFolderName)

	info, err := os.Lstat(target)
	switch {
	case err == nil && info.Mode()&os.ModeSymlink != 0:
		cur, _ := os.Readlink(target)
		if cur == root {
			fmt.Println("already linked:", target, "->", root)
			return nil
		}
		return fmt.Errorf("%s is a symlink to %s; remove it first", target, cur)
	case err == nil:
		return fmt.Errorf("%s already exists and is not a symlink; move or delete it first", target)
	case !errors.Is(err, os.ErrNotExist):
		return err
	}

	if err := os.Symlink(root, target); err != nil {
		return err
	}
	fmt.Println("linked:", target, "->", root)
	return nil
}

func showLog(args []string) error {
	fs := flag.NewFlagSet("log", flag.ExitOnError)
	all := fs.Bool("all", false, "print every error/exception, not only CoupleBeds ones")
	fs.Parse(args)

	path := filepath.Join(home(), ".config", "unity3d", "Ludeon Studios",
		"RimWorld by Ludeon Studios", "Player.log")
	f, err := os.Open(path)
	if err != nil {
		return err
	}
	defer f.Close()

	sc := bufio.NewScanner(f)
	sc.Buffer(make([]byte, 1024*1024), 16*1024*1024)
	found := 0
	for sc.Scan() {
		line := sc.Text()
		hit := strings.Contains(line, modFolderName)
		if *all {
			hit = hit || strings.Contains(line, "Exception") || strings.Contains(line, "Error")
		}
		if hit {
			fmt.Println(line)
			found++
		}
	}
	if err := sc.Err(); err != nil {
		return err
	}
	if found == 0 {
		fmt.Println("no matching lines in", path)
	}
	return nil
}
