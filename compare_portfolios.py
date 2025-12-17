"""
Portfolio Comparison Tool
Compares hedging simulation results between student and reference implementations.
"""

import json
import argparse
import sys
from dataclasses import dataclass, field
from typing import List, Dict, Optional, Tuple
from datetime import datetime
from pathlib import Path
import math

# Try to import optional dependencies
try:
    from rich.console import Console
    from rich.table import Table
    from rich import box
    RICH_AVAILABLE = True
except ImportError:
    RICH_AVAILABLE = False
    print("Warning: 'rich' not installed. Install with: pip install rich")
    print("Falling back to basic output.\n")


# ============================================================================
# Data Classes
# ============================================================================

@dataclass
class PortfolioEntry:
    """Represents a single portfolio state at a given date."""
    date: datetime
    value: float
    deltas: List[float]
    deltasStdDev: List[float]
    price: float
    priceStdDev: float

    @classmethod
    def from_dict(cls, data: dict) -> 'PortfolioEntry':
        """Create PortfolioEntry from JSON dictionary."""
        return cls(
            date=datetime.fromisoformat(data['date']),
            value=data['value'],
            deltas=data.get('deltas', []),
            deltasStdDev=data.get('deltasStdDev', []),
            price=data['price'],
            priceStdDev=data['priceStdDev']
        )


@dataclass
class ComparisonFailure:
    """Represents a failed comparison."""
    date: datetime
    type: str  # "Price" or "Delta[i]"
    our_value: float
    ref_value: float
    our_stddev: float
    ref_stddev: float
    difference: float
    tolerance: float
    
    def __str__(self) -> str:
        return (f"{self.date.strftime('%Y-%m-%d')} | {self.type:12s} | "
                f"Ours: {self.our_value:9.6f} ± {self.our_stddev:.6f} | "
                f"Ref: {self.ref_value:9.6f} ± {self.ref_stddev:.6f} | "
                f"Diff: {self.difference:8.6f} > Tol: {self.tolerance:.6f}")


@dataclass
class PortfolioValueStats:
    """Statistics about portfolio value differences."""
    mean_absolute_diff: float = 0.0
    max_absolute_diff: float = 0.0
    max_diff_date: Optional[datetime] = None
    mean_relative_diff: float = 0.0  # as percentage
    max_relative_diff: float = 0.0  # as percentage
    max_relative_diff_date: Optional[datetime] = None
    initial_value_diff: float = 0.0
    final_value_diff: float = 0.0
    rmse: float = 0.0  # Root Mean Square Error


@dataclass
class ValidationStatistics:
    """Statistics about the validation results."""
    total_dates: int = 0
    price_passed: int = 0
    price_failed: int = 0
    total_deltas: int = 0
    deltas_passed: int = 0
    deltas_failed: int = 0
    delta_failures_by_asset: Dict[int, int] = field(default_factory=dict)
    value_stats: PortfolioValueStats = field(default_factory=PortfolioValueStats)
    
    @property
    def price_success_rate(self) -> float:
        if self.total_dates == 0:
            return 0.0
        return self.price_passed / self.total_dates
    
    @property
    def delta_success_rate(self) -> float:
        if self.total_deltas == 0:
            return 0.0
        return self.deltas_passed / self.total_deltas
    
    @property
    def overall_success_rate(self) -> float:
        total = self.total_dates + self.total_deltas
        if total == 0:
            return 0.0
        passed = self.price_passed + self.deltas_passed
        return passed / total


@dataclass
class ValidationResult:
    """Complete validation result."""
    critical_errors: List[str] = field(default_factory=list)
    warnings: List[str] = field(default_factory=list)
    price_failures: List[ComparisonFailure] = field(default_factory=list)
    delta_failures: List[ComparisonFailure] = field(default_factory=list)
    stats: ValidationStatistics = field(default_factory=ValidationStatistics)
    
    @property
    def is_critical(self) -> bool:
        return len(self.critical_errors) > 0
    
    @property
    def is_acceptable(self) -> bool:
        return not self.is_critical and self.stats.overall_success_rate >= 0.80
    
    @property
    def exit_code(self) -> int:
        if self.is_critical:
            return 3
        rate = self.stats.overall_success_rate
        if rate >= 0.80:
            return 0
        elif rate >= 0.50:
            return 2
        else:
            return 3


# ============================================================================
# Core Comparison Logic
# ============================================================================

def check_confidence_interval(
    our_value: float,
    ref_value: float,
    our_stddev: float,
    ref_stddev: float,
    z_score: float = 1.96
) -> Tuple[bool, float, float]:
    """
    Check if two confidence intervals overlap.
    
    Returns:
        (overlaps, difference, tolerance)
    """
    difference = abs(our_value - ref_value)
    tolerance = z_score * (our_stddev + ref_stddev)
    overlaps = difference <= tolerance
    return overlaps, difference, tolerance


def compute_portfolio_value_stats(
    our_portfolio: List[PortfolioEntry],
    ref_portfolio: List[PortfolioEntry],
    common_dates: List[datetime]
) -> PortfolioValueStats:
    """
    Compute statistics comparing portfolio values.
    """
    stats = PortfolioValueStats()
    
    our_dict = {entry.date: entry for entry in our_portfolio}
    ref_dict = {entry.date: entry for entry in ref_portfolio}
    
    abs_diffs = []
    rel_diffs = []
    squared_diffs = []
    
    for date in common_dates:
        our_val = our_dict[date].value
        ref_val = ref_dict[date].value
        
        abs_diff = abs(our_val - ref_val)
        abs_diffs.append(abs_diff)
        squared_diffs.append(abs_diff ** 2)
        
        # Track maximum absolute difference
        if abs_diff > stats.max_absolute_diff:
            stats.max_absolute_diff = abs_diff
            stats.max_diff_date = date
        
        # Relative difference (as percentage)
        if abs(ref_val) > 1e-10:  # Avoid division by zero
            rel_diff = 100 * abs_diff / abs(ref_val)
            rel_diffs.append(rel_diff)
            
            if rel_diff > stats.max_relative_diff:
                stats.max_relative_diff = rel_diff
                stats.max_relative_diff_date = date
    
    # Compute means
    if abs_diffs:
        stats.mean_absolute_diff = sum(abs_diffs) / len(abs_diffs)
    
    if rel_diffs:
        stats.mean_relative_diff = sum(rel_diffs) / len(rel_diffs)
    
    if squared_diffs:
        stats.rmse = math.sqrt(sum(squared_diffs) / len(squared_diffs))
    
    # Initial and final differences
    if common_dates:
        first_date = min(common_dates)
        last_date = max(common_dates)
        
        stats.initial_value_diff = abs(
            our_dict[first_date].value - ref_dict[first_date].value
        )
        stats.final_value_diff = abs(
            our_dict[last_date].value - ref_dict[last_date].value
        )
    
    return stats


def load_portfolio(filepath: str) -> List[PortfolioEntry]:
    """Load and parse portfolio JSON file."""
    try:
        with open(filepath, 'r') as f:
            data = json.load(f)
        
        if not isinstance(data, list):
            raise ValueError("Portfolio file must contain a JSON array")
        
        entries = []
        for idx, entry_data in enumerate(data):
            try:
                entry = PortfolioEntry.from_dict(entry_data)
                entries.append(entry)
            except Exception as e:
                raise ValueError(f"Invalid entry at index {idx}: {e}")
        
        return entries
    
    except FileNotFoundError:
        raise FileNotFoundError(f"Portfolio file not found: {filepath}")
    except json.JSONDecodeError as e:
        raise ValueError(f"Invalid JSON in {filepath}: {e}")


def validate_portfolio_structure(
    our_portfolio: List[PortfolioEntry],
    ref_portfolio: List[PortfolioEntry],
    result: ValidationResult
) -> bool:
    """Validate that both portfolios have compatible structure."""
    
    if len(our_portfolio) == 0:
        result.critical_errors.append("Our portfolio is empty")
        return False
    
    if len(ref_portfolio) == 0:
        result.critical_errors.append("Reference portfolio is empty")
        return False
    
    # Check number of assets (from first entry)
    our_n_assets = len(our_portfolio[0].deltas)
    ref_n_assets = len(ref_portfolio[0].deltas)
    
    if our_n_assets != ref_n_assets:
        result.critical_errors.append(
            f"Number of assets differs: ours={our_n_assets}, ref={ref_n_assets}"
        )
        return False
    
    # Build date sets for comparison
    our_dates = {entry.date for entry in our_portfolio}
    ref_dates = {entry.date for entry in ref_portfolio}
    
    only_in_ours = our_dates - ref_dates
    only_in_ref = ref_dates - our_dates
    
    if only_in_ours:
        dates_str = ', '.join(d.strftime('%Y-%m-%d') for d in sorted(only_in_ours)[:5])
        result.warnings.append(
            f"Dates present in ours but not in reference: {dates_str}" +
            (f" and {len(only_in_ours)-5} more..." if len(only_in_ours) > 5 else "")
        )
    
    if only_in_ref:
        dates_str = ', '.join(d.strftime('%Y-%m-%d') for d in sorted(only_in_ref)[:5])
        result.warnings.append(
            f"Dates present in reference but not in ours: {dates_str}" +
            (f" and {len(only_in_ref)-5} more..." if len(only_in_ref) > 5 else "")
        )
    
    return True


def compare_portfolios(
    our_portfolio: List[PortfolioEntry],
    ref_portfolio: List[PortfolioEntry],
    confidence_level: float = 0.95,
    tolerance_factor: float = 1.0
) -> ValidationResult:
    """
    Compare two portfolios and return validation results.
    
    Args:
        our_portfolio: Our implementation results
        ref_portfolio: Reference implementation results
        confidence_level: Confidence level for intervals (default 0.95)
        tolerance_factor: Additional tolerance multiplier
    """
    result = ValidationResult()
    
    # Validate structure
    if not validate_portfolio_structure(our_portfolio, ref_portfolio, result):
        return result
    
    # Calculate z-score from confidence level
    # For 95%: z = 1.96, for 99%: z = 2.576
    z_score = 1.96 if confidence_level == 0.95 else 2.576
    z_score *= tolerance_factor
    
    # Create dictionaries for easy lookup
    our_dict = {entry.date: entry for entry in our_portfolio}
    ref_dict = {entry.date: entry for entry in ref_portfolio}
    
    # Get common dates
    common_dates = sorted(set(our_dict.keys()) & set(ref_dict.keys()))
    
    result.stats.total_dates = len(common_dates)
    
    # Compute portfolio value statistics
    result.stats.value_stats = compute_portfolio_value_stats(
        our_portfolio, ref_portfolio, common_dates
    )
    
    # Compare each date
    for date in common_dates:
        our_entry = our_dict[date]
        ref_entry = ref_dict[date]
        
        # Compare price
        overlaps, diff, tol = check_confidence_interval(
            our_entry.price,
            ref_entry.price,
            our_entry.priceStdDev,
            ref_entry.priceStdDev,
            z_score
        )
        
        if overlaps:
            result.stats.price_passed += 1
        else:
            result.stats.price_failed += 1
            result.price_failures.append(ComparisonFailure(
                date=date,
                type="Price",
                our_value=our_entry.price,
                ref_value=ref_entry.price,
                our_stddev=our_entry.priceStdDev,
                ref_stddev=ref_entry.priceStdDev,
                difference=diff,
                tolerance=tol
            ))
        
        # Compare deltas
        n_assets = min(len(our_entry.deltas), len(ref_entry.deltas))
        
        if len(our_entry.deltas) != len(ref_entry.deltas):
            result.warnings.append(
                f"Date {date.strftime('%Y-%m-%d')}: Different number of deltas "
                f"(ours={len(our_entry.deltas)}, ref={len(ref_entry.deltas)})"
            )
        
        # Check if we have stddev for deltas
        has_our_stddev = len(our_entry.deltasStdDev) >= n_assets
        has_ref_stddev = len(ref_entry.deltasStdDev) >= n_assets
        
        for i in range(n_assets):
            result.stats.total_deltas += 1
            
            our_delta = our_entry.deltas[i]
            ref_delta = ref_entry.deltas[i]
            
            # Get stddev if available, otherwise use 0 (deterministic)
            our_stddev = our_entry.deltasStdDev[i] if has_our_stddev else 0.0
            ref_stddev = ref_entry.deltasStdDev[i] if has_ref_stddev else 0.0
            
            # Handle near-zero deltas (both < 1e-6)
            if abs(our_delta) < 1e-6 and abs(ref_delta) < 1e-6:
                result.stats.deltas_passed += 1
                continue
            
            overlaps, diff, tol = check_confidence_interval(
                our_delta,
                ref_delta,
                our_stddev,
                ref_stddev,
                z_score
            )
            
            if overlaps:
                result.stats.deltas_passed += 1
            else:
                result.stats.deltas_failed += 1
                result.stats.delta_failures_by_asset[i] = \
                    result.stats.delta_failures_by_asset.get(i, 0) + 1
                
                result.delta_failures.append(ComparisonFailure(
                    date=date,
                    type=f"Delta[{i}]",
                    our_value=our_delta,
                    ref_value=ref_delta,
                    our_stddev=our_stddev,
                    ref_stddev=ref_stddev,
                    difference=diff,
                    tolerance=tol
                ))
    
    return result


# ============================================================================
# Reporting
# ============================================================================

def generate_report(
    result: ValidationResult,
    verbose: bool = False,
    use_color: bool = True
) -> str:
    """Generate human-readable report."""
    
    if RICH_AVAILABLE and use_color:
        return generate_rich_report(result, verbose)
    else:
        return generate_plain_report(result, verbose)


def generate_plain_report(result: ValidationResult, verbose: bool) -> str:
    """Generate plain text report."""
    lines = []
    lines.append("=" * 80)
    lines.append("PORTFOLIO VALIDATION REPORT".center(80))
    lines.append("=" * 80)
    lines.append("")
    
    # Critical errors
    if result.critical_errors:
        lines.append("[CRITICAL ERRORS]")
        for error in result.critical_errors:
            lines.append(f"  ❌ {error}")
        lines.append("")
    
    # Warnings
    if result.warnings:
        lines.append("[WARNINGS]")
        for warning in result.warnings[:10]:  # Limit to 10
            lines.append(f"  ⚠️  {warning}")
        if len(result.warnings) > 10:
            lines.append(f"  ... and {len(result.warnings) - 10} more warnings")
        lines.append("")
    
    # If critical, stop here
    if result.is_critical:
        lines.append("❌ CRITICAL ERRORS DETECTED - Cannot proceed with comparison")
        return "\n".join(lines)
    
    # Portfolio value comparison
    lines.append("[PORTFOLIO VALUE COMPARISON]")
    vs = result.stats.value_stats
    lines.append(f"  Initial difference:     {vs.initial_value_diff:12.6f}")
    lines.append(f"  Final difference:       {vs.final_value_diff:12.6f}")
    lines.append(f"  Mean absolute diff:     {vs.mean_absolute_diff:12.6f}")
    lines.append(f"  Max absolute diff:      {vs.max_absolute_diff:12.6f}")
    if vs.max_diff_date:
        lines.append(f"    (at {vs.max_diff_date.strftime('%Y-%m-%d')})")
    lines.append(f"  Mean relative diff:     {vs.mean_relative_diff:11.3f}%")
    lines.append(f"  Max relative diff:      {vs.max_relative_diff:11.3f}%")
    if vs.max_relative_diff_date:
        lines.append(f"    (at {vs.max_relative_diff_date.strftime('%Y-%m-%d')})")
    lines.append(f"  RMSE:                   {vs.rmse:12.6f}")
    lines.append("")
    
    # Price comparison
    lines.append("[PRICE COMPARISON]")
    rate = result.stats.price_success_rate
    status = "✅" if rate >= 0.80 else "❌"
    lines.append(f"  {status} Passed: {result.stats.price_passed}/{result.stats.total_dates} "
                f"dates ({rate*100:.1f}%)")
    
    if result.price_failures and not verbose:
        lines.append(f"  ❌ Failed: {len(result.price_failures)} dates (use --verbose for details)")
    lines.append("")
    
    # Delta comparison
    lines.append("[DELTA COMPARISON]")
    rate = result.stats.delta_success_rate
    status = "✅" if rate >= 0.80 else "❌"
    lines.append(f"  {status} Passed: {result.stats.deltas_passed}/{result.stats.total_deltas} "
                f"deltas ({rate*100:.1f}%)")
    
    if result.stats.delta_failures_by_asset:
        lines.append("  Failed by asset:")
        for asset_idx in sorted(result.stats.delta_failures_by_asset.keys()):
            count = result.stats.delta_failures_by_asset[asset_idx]
            lines.append(f"    Asset {asset_idx}: {count} failures")
    lines.append("")
    
    # Detailed failures
    if verbose and (result.price_failures or result.delta_failures):
        lines.append("[DETAILED FAILURES]")
        lines.append("")
        
        # Group by date
        failures_by_date = {}
        for failure in result.price_failures + result.delta_failures:
            if failure.date not in failures_by_date:
                failures_by_date[failure.date] = []
            failures_by_date[failure.date].append(failure)
        
        for date in sorted(failures_by_date.keys()):
            lines.append(f"Date: {date.strftime('%Y-%m-%d')}")
            for failure in failures_by_date[date]:
                lines.append(f"  {failure}")
            lines.append("")
    
    # Summary
    lines.append("=" * 80)
    lines.append("[SUMMARY]")
    rate = result.stats.overall_success_rate
    lines.append(f"Overall success rate: {rate*100:.1f}%")
    
    if result.is_acceptable:
        lines.append("Recommendation: ✅ ACCEPTABLE")
    elif rate >= 0.50:
        lines.append("Recommendation: ⚠️  REVIEW IMPLEMENTATION")
    else:
        lines.append("Recommendation: ❌ MAJOR ISSUES DETECTED")
    
    lines.append("=" * 80)
    
    return "\n".join(lines)


def generate_rich_report(result: ValidationResult, verbose: bool) -> str:
    """Generate rich formatted report."""
    console = Console()
    
    with console.capture() as capture:
        console.print("\n[bold cyan]=" * 40)
        console.print("[bold cyan]PORTFOLIO VALIDATION REPORT[/bold cyan]", justify="center")
        console.print("[bold cyan]=" * 40 + "\n")
        
        # Critical errors
        if result.critical_errors:
            console.print("[bold red]CRITICAL ERRORS[/bold red]")
            for error in result.critical_errors:
                console.print(f"  [red]❌ {error}[/red]")
            console.print()
        
        # Warnings
        if result.warnings:
            console.print("[bold yellow]WARNINGS[/bold yellow]")
            for warning in result.warnings[:10]:
                console.print(f"  [yellow]⚠️  {warning}[/yellow]")
            if len(result.warnings) > 10:
                console.print(f"  [dim]... and {len(result.warnings) - 10} more warnings[/dim]")
            console.print()
        
        if result.is_critical:
            console.print("[bold red]❌ CRITICAL ERRORS DETECTED - Cannot proceed[/bold red]")
            return capture.get()
        
        # Portfolio value comparison table
        vs = result.stats.value_stats
        value_table = Table(title="Portfolio Value Comparison", box=box.ROUNDED)
        value_table.add_column("Metric", style="cyan")
        value_table.add_column("Value", style="yellow", justify="right")
        value_table.add_column("Details", style="dim")
        
        value_table.add_row(
            "Initial difference",
            f"{vs.initial_value_diff:.6f}",
            ""
        )
        value_table.add_row(
            "Final difference",
            f"{vs.final_value_diff:.6f}",
            ""
        )
        value_table.add_row(
            "Mean absolute diff",
            f"{vs.mean_absolute_diff:.6f}",
            ""
        )
        value_table.add_row(
            "Max absolute diff",
            f"{vs.max_absolute_diff:.6f}",
            f"at {vs.max_diff_date.strftime('%Y-%m-%d')}" if vs.max_diff_date else ""
        )
        value_table.add_row(
            "Mean relative diff",
            f"{vs.mean_relative_diff:.3f}%",
            ""
        )
        value_table.add_row(
            "Max relative diff",
            f"{vs.max_relative_diff:.3f}%",
            f"at {vs.max_relative_diff_date.strftime('%Y-%m-%d')}" if vs.max_relative_diff_date else ""
        )
        value_table.add_row(
            "RMSE",
            f"{vs.rmse:.6f}",
            ""
        )
        
        console.print(value_table)
        console.print()
        
        # Create summary table
        table = Table(title="Price & Delta Comparison Summary", box=box.ROUNDED)
        table.add_column("Category", style="cyan")
        table.add_column("Passed", style="green")
        table.add_column("Failed", style="red")
        table.add_column("Total", style="blue")
        table.add_column("Success Rate", style="magenta")
        
        price_rate = result.stats.price_success_rate
        price_status = "✅" if price_rate >= 0.80 else "❌"
        table.add_row(
            "Prices",
            str(result.stats.price_passed),
            str(result.stats.price_failed),
            str(result.stats.total_dates),
            f"{price_rate*100:.1f}% {price_status}"
        )
        
        delta_rate = result.stats.delta_success_rate
        delta_status = "✅" if delta_rate >= 0.80 else "❌"
        table.add_row(
            "Deltas",
            str(result.stats.deltas_passed),
            str(result.stats.deltas_failed),
            str(result.stats.total_deltas),
            f"{delta_rate*100:.1f}% {delta_status}"
        )
        
        overall_rate = result.stats.overall_success_rate
        overall_status = "✅" if overall_rate >= 0.80 else "❌"
        table.add_row(
            "[bold]Overall[/bold]",
            f"[bold]{result.stats.price_passed + result.stats.deltas_passed}[/bold]",
            f"[bold]{result.stats.price_failed + result.stats.deltas_failed}[/bold]",
            f"[bold]{result.stats.total_dates + result.stats.total_deltas}[/bold]",
            f"[bold]{overall_rate*100:.1f}% {overall_status}[/bold]"
        )
        
        console.print(table)
        console.print()
        
        # Delta failures by asset
        if result.stats.delta_failures_by_asset:
            console.print("[bold]Delta Failures by Asset:[/bold]")
            for asset_idx in sorted(result.stats.delta_failures_by_asset.keys()):
                count = result.stats.delta_failures_by_asset[asset_idx]
                console.print(f"  Asset {asset_idx}: [red]{count}[/red] failures")
            console.print()
        
        # Detailed failures
        if verbose and (result.price_failures or result.delta_failures):
            console.print("[bold yellow]DETAILED FAILURES[/bold yellow]\n")
            
            failures_by_date = {}
            for failure in result.price_failures + result.delta_failures:
                if failure.date not in failures_by_date:
                    failures_by_date[failure.date] = []
                failures_by_date[failure.date].append(failure)
            
            for date in sorted(failures_by_date.keys())[:20]:  # Limit to 20 dates
                console.print(f"[cyan]Date: {date.strftime('%Y-%m-%d')}[/cyan]")
                for failure in failures_by_date[date]:
                    console.print(f"  [red]{failure}[/red]")
                console.print()
            
            if len(failures_by_date) > 20:
                console.print(f"[dim]... and {len(failures_by_date) - 20} more dates with failures[/dim]\n")
        
        # Final recommendation
        console.print("[bold cyan]=" * 40)
        console.print("[bold]FINAL VERDICT[/bold]")
        
        if result.is_acceptable:
            console.print("[bold green]✅ ACCEPTABLE - Implementation validated[/bold green]")
        elif overall_rate >= 0.50:
            console.print("[bold yellow]⚠️  REVIEW NEEDED - Some discrepancies detected[/bold yellow]")
        else:
            console.print("[bold red]❌ FAILED - Major issues detected[/bold red]")
        
        console.print(f"Overall success rate: [bold]{overall_rate*100:.1f}%[/bold]")
        console.print("[bold cyan]=" * 40 + "\n")
    
    return capture.get()


def export_failures_json(result: ValidationResult, filepath: str):
    """Export failures to JSON file for further analysis."""
    data = {
        "summary": {
            "total_dates": result.stats.total_dates,
            "price_passed": result.stats.price_passed,
            "price_failed": result.stats.price_failed,
            "total_deltas": result.stats.total_deltas,
            "deltas_passed": result.stats.deltas_passed,
            "deltas_failed": result.stats.deltas_failed,
            "overall_success_rate": result.stats.overall_success_rate,
            "is_acceptable": result.is_acceptable
        },
        "portfolio_value_stats": {
            "initial_value_diff": result.stats.value_stats.initial_value_diff,
            "final_value_diff": result.stats.value_stats.final_value_diff,
            "mean_absolute_diff": result.stats.value_stats.mean_absolute_diff,
            "max_absolute_diff": result.stats.value_stats.max_absolute_diff,
            "max_diff_date": result.stats.value_stats.max_diff_date.isoformat() if result.stats.value_stats.max_diff_date else None,
            "mean_relative_diff": result.stats.value_stats.mean_relative_diff,
            "max_relative_diff": result.stats.value_stats.max_relative_diff,
            "max_relative_diff_date": result.stats.value_stats.max_relative_diff_date.isoformat() if result.stats.value_stats.max_relative_diff_date else None,
            "rmse": result.stats.value_stats.rmse
        },
        "critical_errors": result.critical_errors,
        "warnings": result.warnings,
        "price_failures": [
            {
                "date": f.date.isoformat(),
                "our_value": f.our_value,
                "ref_value": f.ref_value,
                "difference": f.difference,
                "tolerance": f.tolerance
            }
            for f in result.price_failures
        ],
        "delta_failures": [
            {
                "date": f.date.isoformat(),
                "type": f.type,
                "our_value": f.our_value,
                "ref_value": f.ref_value,
                "difference": f.difference,
                "tolerance": f.tolerance
            }
            for f in result.delta_failures
        ]
    }
    
    with open(filepath, 'w') as f:
        json.dump(data, f, indent=2)


# ============================================================================
# CLI
# ============================================================================

def main():
    parser = argparse.ArgumentParser(
        description="Compare hedging portfolio results between implementations",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  %(prog)s our_portfolio.json ref_portfolio.json
  %(prog)s our_portfolio.json ref_portfolio.json --verbose
  %(prog)s our_portfolio.json ref_portfolio.json --output report.txt --export-json failures.json
        """
    )
    
    parser.add_argument(
        'our_file',
        help='Path to our portfolio JSON file'
    )
    
    parser.add_argument(
        'ref_file',
        help='Path to reference portfolio JSON file'
    )
    
    parser.add_argument(
        '--confidence',
        type=float,
        default=0.95,
        choices=[0.95, 0.99],
        help='Confidence level for intervals (default: 0.95)'
    )
    
    parser.add_argument(
        '--tolerance',
        type=float,
        default=1.0,
        help='Additional tolerance multiplier (default: 1.0)'
    )
    
    parser.add_argument(
        '--output', '-o',
        help='Output report to file instead of stdout'
    )
    
    parser.add_argument(
        '--export-json',
        help='Export failures to JSON file'
    )
    
    parser.add_argument(
        '--verbose', '-v',
        action='store_true',
        help='Show detailed failure information'
    )
    
    parser.add_argument(
        '--quiet', '-q',
        action='store_true',
        help='Quiet mode (minimal output)'
    )
    
    parser.add_argument(
        '--no-color',
        action='store_true',
        help='Disable colored output'
    )
    
    args = parser.parse_args()
    
    # Validate arguments
    if args.verbose and args.quiet:
        parser.error("Cannot use --verbose and --quiet together")
    
    try:
        # Load portfolios
        print(f"Loading our portfolio: {args.our_file}")
        our_portfolio = load_portfolio(args.our_file)
        print(f"  ✓ Loaded {len(our_portfolio)} entries")
        
        print(f"Loading reference portfolio: {args.ref_file}")
        ref_portfolio = load_portfolio(args.ref_file)
        print(f"  ✓ Loaded {len(ref_portfolio)} entries")
        print()
        
        # Compare
        print("Comparing portfolios...")
        result = compare_portfolios(
            our_portfolio,
            ref_portfolio,
            confidence_level=args.confidence,
            tolerance_factor=args.tolerance
        )
        print("  ✓ Comparison complete\n")
        
        # Generate report
        report = generate_report(
            result,
            verbose=args.verbose and not args.quiet,
            use_color=not args.no_color
        )
        
        # Output report
        if args.output:
            with open(args.output, 'w') as f:
                f.write(report)
            print(f"Report written to: {args.output}")
        else:
            print(report)
        
        # Export JSON if requested
        if args.export_json:
            export_failures_json(result, args.export_json)
            print(f"Failures exported to: {args.export_json}")
        
        # Return appropriate exit code
        sys.exit(result.exit_code)
        
    except FileNotFoundError as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(3)
    except ValueError as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(3)
    except Exception as e:
        print(f"Unexpected error: {e}", file=sys.stderr)
        import traceback
        traceback.print_exc()
        sys.exit(3)


if __name__ == "__main__":
    main()