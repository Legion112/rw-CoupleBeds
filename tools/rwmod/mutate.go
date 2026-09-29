package main

// mutate is a small mutation tester for CoupleBeds.Core.
//
// It breaks the decision logic in one specific way at a time and checks that
// `rwmod test` notices. A mutant that survives means the suite has a blind spot
// there. The edits are applied to the working tree and always restored, so run
// it on a clean tree if you want to be able to tell.

import (
	"errors"
	"flag"
	"fmt"
	"os"
	"os/exec"
	"os/signal"
	"path/filepath"
	"regexp"
	"strconv"
	"strings"
)

const (
	scorerFile  = "Source/Core/BedScorer.cs"
	plannerFile = "Source/Core/CoupleBedPlanner.cs"
	matcherFile = "Source/Core/PartnerMatcher.cs"
)

type mutant struct {
	label string
	file  string
	old   string
	new   string
}

// Each entry is a plausible way to get the logic subtly wrong.
var mutants = []mutant{
	{"private-room bonus removed", scorerFile,
		"PrivateRoomBonus = 40f", "PrivateRoomBonus = 0f"},
	{"barracks penalty removed", scorerFile,
		"PenaltyPerOtherBed = -10f", "PenaltyPerOtherBed = 0f"},
	{"comfort ignored", scorerFile,
		"ComfortWeight = 20f", "ComfortWeight = 0f"},
	{"stay-put bonus removed", scorerFile,
		"AlreadyOwnedBonus = 5f", "AlreadyOwnedBonus = 0f"},
	{"outdoor penalty removed", scorerFile,
		"OutdoorsOrNoRoomPenalty = -100f", "OutdoorsOrNoRoomPenalty = 0f"},

	{"prisoner beds become candidates", plannerFile,
		"if (bed.Medical || bed.ForPrisoners) continue;", "if (bed.Medical) continue;"},
	{"single beds become candidates", plannerFile,
		"if (bed.SleepingSlots < 2) continue;", "if (bed.SleepingSlots < 1) continue;"},
	{"animal beds become candidates", plannerFile,
		"if (!bed.Humanlike) continue;", "if (false) continue;"},
	{"later bed wins a score tie", plannerFile,
		"if (score > bestScore)", "if (score >= bestScore)"},
	{"upgrade margin comparison flipped", plannerFile,
		"if (bestScore < currentScore + settings.UpgradeMargin) return null;",
		"if (bestScore <= currentScore + settings.UpgradeMargin) return null;"},
	{"slave/colonist mixing allowed", plannerFile,
		"if (a.IsSlave != b.IsSlave) continue;", "if (false) continue;"},
	{"third-party beds may be stolen", plannerFile,
		"if (current[i] != a.Id && current[i] != b.Id) return false;", "if (false) return false;"},
	{"bed owner-type ignored", plannerFile,
		"if (bed.ForOwnerType != wanted) return false;", "if (false) return false;"},
	{"mutual-partner requirement dropped", plannerFile,
		"if (PartnerMatcher.PartnerOf(b, settings) != a.Id) continue; // must be mutual",
		"if (false) continue; // must be mutual"},
	{"pathfinding checked before the cheap filters", plannerFile,
		`            if (!access.CanUseBedEver(a.Id, bed.Id) || !access.CanUseBedEver(b.Id, bed.Id)) return false;
            if (access.IsForbidden(a.Id, bed.Id) || access.IsForbidden(b.Id, bed.Id)) return false;
            if (!access.CanReach(a.Id, bed.Id)) return false;
            if (!access.CanReach(b.Id, bed.Id)) return false;`,
		`            if (!access.CanReach(a.Id, bed.Id)) return false;
            if (!access.CanReach(b.Id, bed.Id)) return false;
            if (!access.CanUseBedEver(a.Id, bed.Id) || !access.CanUseBedEver(b.Id, bed.Id)) return false;
            if (access.IsForbidden(a.Id, bed.Id) || access.IsForbidden(b.Id, bed.Id)) return false;`},

	{"opinion tie goes to the last relation", matcherFile,
		"if (!found || r.Opinion > bestOpinion)", "if (!found || r.Opinion >= bestOpinion)"},
	{"dead partners considered", matcherFile,
		"if (r.PartnerDead) continue;", "if (false) continue;"},
	{"spouses-only filter dropped", matcherFile,
		"if (!settings.IncludeLovers && !best.IsSpouse) return PawnView.NoPawn;",
		"if (false) return PawnView.NoPawn;"},
	{"deathrest gene ignored", matcherFile,
		"if (pawn.HasDeathrestGene) return false;", "if (false) return false;"},
	{"downed pawns managed", matcherFile,
		"if (pawn.Dead || pawn.Downed) return false;", "if (pawn.Dead) return false;"},
}

var resultLine = regexp.MustCompile(`Failed:\s+(\d+), Passed:\s+(\d+)`)

func mutate(args []string) error {
	fs := flag.NewFlagSet("mutate", flag.ExitOnError)
	only := fs.String("run", "", "only mutants whose label contains this substring")
	fs.Parse(args)

	root, err := repoRoot()
	if err != nil {
		return err
	}
	dotnet, err := findDotnet()
	if err != nil {
		return err
	}

	failed, passed, err := runSuite(dotnet, root)
	if err != nil {
		return fmt.Errorf("baseline: %w", err)
	}
	if failed != 0 {
		return fmt.Errorf("baseline is not green (%d failed); fix the tests first", failed)
	}
	fmt.Printf("baseline: %d passed\n\n", passed)

	selected := mutants[:0:0]
	for _, m := range mutants {
		if *only == "" || strings.Contains(strings.ToLower(m.label), strings.ToLower(*only)) {
			selected = append(selected, m)
		}
	}
	if len(selected) == 0 {
		return fmt.Errorf("no mutant matches %q", *only)
	}

	var survivors []string
	for _, m := range selected {
		label, err := applyAndTest(dotnet, root, m)
		if err != nil {
			return err
		}
		if label != "" {
			survivors = append(survivors, label)
		}
	}

	fmt.Println()
	if len(survivors) > 0 {
		fmt.Printf("%d of %d mutants survived:\n", len(survivors), len(selected))
		for _, s := range survivors {
			fmt.Println("  -", s)
		}
		return errors.New("the test suite has blind spots")
	}
	fmt.Printf("all %d mutants caught\n", len(selected))
	return nil
}

// applyAndTest returns the mutant's label if it survived, or "" if it was caught.
func applyAndTest(dotnet, root string, m mutant) (survivor string, err error) {
	path := filepath.Join(root, m.file)
	original, err := os.ReadFile(path)
	if err != nil {
		return "", err
	}
	text := string(original)
	if !strings.Contains(text, m.old) {
		fmt.Printf("  STALE     %s (pattern no longer in %s)\n", m.label, m.file)
		return m.label + " (stale pattern)", nil
	}

	info, err := os.Stat(path)
	if err != nil {
		return "", err
	}
	// Put the file back whatever happens, including on Ctrl-C.
	restore := func() { os.WriteFile(path, original, info.Mode().Perm()) }
	stop := make(chan os.Signal, 1)
	signal.Notify(stop, os.Interrupt)
	go func() {
		<-stop
		restore()
		os.Exit(130)
	}()
	defer func() {
		signal.Stop(stop)
		restore()
	}()

	mutated := strings.Replace(text, m.old, m.new, 1)
	if err := os.WriteFile(path, []byte(mutated), info.Mode().Perm()); err != nil {
		return "", err
	}

	failed, passed, err := runSuite(dotnet, root)
	if err != nil {
		// A mutant that does not compile tells us nothing either way.
		fmt.Printf("  INVALID   %s (%v)\n", m.label, err)
		return m.label + " (did not build)", nil
	}
	if failed > 0 {
		fmt.Printf("  caught    %s  (%d failed)\n", m.label, failed)
		return "", nil
	}
	fmt.Printf("  SURVIVED  %s  (%d passed, nothing noticed)\n", m.label, passed)
	return m.label, nil
}

func runSuite(dotnet, root string) (failed, passed int, err error) {
	cmd := exec.Command(dotnet, "test", filepath.Join("Tests", "CoupleBeds.Tests.csproj"),
		"-nologo", "-v", "q")
	cmd.Dir = root
	out, runErr := cmd.CombinedOutput()
	text := string(out)

	if strings.Contains(text, "error CS") {
		return 0, 0, errors.New("compile error")
	}
	m := resultLine.FindStringSubmatch(text)
	if m == nil {
		if runErr != nil {
			return 0, 0, fmt.Errorf("dotnet test: %w", runErr)
		}
		return 0, 0, errors.New("could not parse the test summary")
	}
	failed, _ = strconv.Atoi(m[1])
	passed, _ = strconv.Atoi(m[2])
	return failed, passed, nil
}
