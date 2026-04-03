// src/components/PrimeRadiant/PlanetNav.test.tsx
// TDD: Planet navigation — comprehensive coverage

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { PlanetNav } from './PlanetNav';

describe('PlanetNav', () => {
  const mockNavigate = vi.fn();
  const mockResetView = vi.fn();
  const mockLaunchLunarLander = vi.fn();
  const mockLoadArcGIS = vi.fn();
  const mockRemoveArcGIS = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ── Rendering ──

  it('renders the toggle button', () => {
    render(<PlanetNav onNavigateToPlanet={mockNavigate} />);
    expect(screen.getByRole('button', { name: /toggle planet navigation/i })).toBeInTheDocument();
  });

  it('does not show planet list when collapsed', () => {
    render(<PlanetNav onNavigateToPlanet={mockNavigate} />);
    expect(screen.queryByText('Sun')).not.toBeInTheDocument();
  });

  it('shows planet list when toggle is clicked', () => {
    render(<PlanetNav onNavigateToPlanet={mockNavigate} />);
    fireEvent.click(screen.getByRole('button', { name: /toggle planet navigation/i }));
    expect(screen.getByText('Sun')).toBeInTheDocument();
    expect(screen.getByText('Earth')).toBeInTheDocument();
    expect(screen.getByText('Mars')).toBeInTheDocument();
  });

  it('renders all 11 celestial bodies', () => {
    render(<PlanetNav onNavigateToPlanet={mockNavigate} />);
    fireEvent.click(screen.getByRole('button', { name: /toggle planet navigation/i }));
    const names = ['Demerzel', 'Sun', 'Mercury', 'Venus', 'Earth', 'Moon', 'Mars', 'Jupiter', 'Saturn', 'Uranus', 'Neptune'];
    for (const name of names) {
      expect(screen.getByText(name)).toBeInTheDocument();
    }
  });

  // ── Navigation ──

  it('calls onNavigateToPlanet with correct target when planet clicked', () => {
    render(<PlanetNav onNavigateToPlanet={mockNavigate} />);
    fireEvent.click(screen.getByRole('button', { name: /toggle planet navigation/i }));
    fireEvent.click(screen.getByTitle('Navigate to Earth'));
    expect(mockNavigate).toHaveBeenCalledWith('earth');
  });

  it('calls onNavigateToPlanet for each planet with correct target', () => {
    render(<PlanetNav onNavigateToPlanet={mockNavigate} />);
    fireEvent.click(screen.getByRole('button', { name: /toggle planet navigation/i }));

    const targets = [
      ['Demerzel', 'demerzel-head'],
      ['Sun', 'sun'],
      ['Mercury', 'mercury'],
      ['Venus', 'venus'],
      ['Earth', 'earth'],
      ['Moon', 'moon'],
      ['Mars', 'mars'],
      ['Jupiter', 'jupiter'],
      ['Saturn', 'saturn'],
      ['Uranus', 'uranus'],
      ['Neptune', 'neptune'],
    ] as const;

    for (const [name, target] of targets) {
      fireEvent.click(screen.getByTitle(`Navigate to ${name}`));
      expect(mockNavigate).toHaveBeenCalledWith(target);
    }
    expect(mockNavigate).toHaveBeenCalledTimes(11);
  });

  // ── Reset View ──

  it('shows Reset View button when onResetView is provided', () => {
    render(<PlanetNav onNavigateToPlanet={mockNavigate} onResetView={mockResetView} />);
    fireEvent.click(screen.getByRole('button', { name: /toggle planet navigation/i }));
    expect(screen.getByTitle('Reset to default view')).toBeInTheDocument();
  });

  it('does not show Reset View when onResetView is not provided', () => {
    render(<PlanetNav onNavigateToPlanet={mockNavigate} />);
    fireEvent.click(screen.getByRole('button', { name: /toggle planet navigation/i }));
    expect(screen.queryByTitle('Reset to default view')).not.toBeInTheDocument();
  });

  it('calls onResetView when Reset View clicked', () => {
    render(<PlanetNav onNavigateToPlanet={mockNavigate} onResetView={mockResetView} />);
    fireEvent.click(screen.getByRole('button', { name: /toggle planet navigation/i }));
    fireEvent.click(screen.getByTitle('Reset to default view'));
    expect(mockResetView).toHaveBeenCalledOnce();
  });

  // ── Lunar Lander ──

  it('shows play button next to Moon when onLaunchLunarLander is provided', () => {
    render(<PlanetNav onNavigateToPlanet={mockNavigate} onLaunchLunarLander={mockLaunchLunarLander} />);
    fireEvent.click(screen.getByRole('button', { name: /toggle planet navigation/i }));
    expect(screen.getByTitle(/apollo lm simulator/i)).toBeInTheDocument();
  });

  it('calls onLaunchLunarLander when play button clicked', () => {
    render(<PlanetNav onNavigateToPlanet={mockNavigate} onLaunchLunarLander={mockLaunchLunarLander} />);
    fireEvent.click(screen.getByRole('button', { name: /toggle planet navigation/i }));
    fireEvent.click(screen.getByTitle(/apollo lm simulator/i));
    expect(mockLaunchLunarLander).toHaveBeenCalledOnce();
    expect(mockNavigate).not.toHaveBeenCalled(); // shouldn't trigger navigation
  });

  // ── Toggle state ──

  it('adds active class when expanded', () => {
    render(<PlanetNav onNavigateToPlanet={mockNavigate} />);
    const toggle = screen.getByRole('button', { name: /toggle planet navigation/i });
    expect(toggle.className).not.toContain('active');
    fireEvent.click(toggle);
    expect(toggle.className).toContain('active');
  });

  it('collapses when toggle clicked again', () => {
    render(<PlanetNav onNavigateToPlanet={mockNavigate} />);
    const toggle = screen.getByRole('button', { name: /toggle planet navigation/i });
    fireEvent.click(toggle); // expand
    expect(screen.getByText('Earth')).toBeInTheDocument();
    fireEvent.click(toggle); // collapse
    expect(screen.queryByText('Earth')).not.toBeInTheDocument();
  });

  // ── GIS Layers ──

  it('shows Earth Layers section when onLoadArcGIS is provided', () => {
    render(<PlanetNav onNavigateToPlanet={mockNavigate} onLoadArcGIS={mockLoadArcGIS} onRemoveArcGIS={mockRemoveArcGIS} />);
    fireEvent.click(screen.getByRole('button', { name: /toggle planet navigation/i }));
    expect(screen.getByText(/earth layers/i)).toBeInTheDocument();
  });

  it('does not show Earth Layers when onLoadArcGIS is not provided', () => {
    render(<PlanetNav onNavigateToPlanet={mockNavigate} />);
    fireEvent.click(screen.getByRole('button', { name: /toggle planet navigation/i }));
    expect(screen.queryByText(/earth layers/i)).not.toBeInTheDocument();
  });
});
