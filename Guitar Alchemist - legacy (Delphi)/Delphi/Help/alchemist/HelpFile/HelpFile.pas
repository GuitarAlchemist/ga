{ Turbo Pascal Unit:  HelpFile.pas                  }
{                                                 }
{ This is an interface unit containing integer    }
{ mappings of Topic IDs (names of Help            }
{ Topics) which are located in HelpFile.rtf     }
{                                                 }
{ This file is re-written by RoboHelp           }
{ whenever HelpFile.rtf is saved.   	          }
{                                                 }
{ However, the numeric values stored in           }
{ HelpFile.hh are the 'master values' and if you    }
{ modify the value in HelpFile.hh and then          }
{ save the HelpFile.rtf again, this file will }
{ reflect the changed values.                     }
{                                                 }

Unit HelpFile;
   Interface
   Const
	Why_Guitar_Alchemist_ = 1;
	System_Requirements = 2;
	How_to_buy_Guitar_Alchemist = 3;
	The_main_window = 4;
	The_options_dialog = 5;
	The_fingerboard_options_page = 6;
	The_theme_options_page = 7;
	The_MIDI_options_page = 8;
	View_selector = 9;
	The_menu = 10;
	The_fingerboard = 11;
	The_status_bar = 12;
	The_scale_selector_panel = 13;
	The_scale_pattern_panel = 14;
	The_matching_chords_panel = 15;
	The_chord_selector_panel = 16;
	The_chord_dictionnary_panel = 17;
	The_matching_scale_panel = 18;
	The_scales_view = 19;
	The_chords_view = 20;
	The_intervals = 21;
	The_keys = 22;
	The_major_scale_modes = 23;
	The_melodic_minor_modes = 24;
	The_harmonic_minor_modes = 25;
	The_harmonic_major_modes = 26;
	The_character_tones = 27;
	Implementation
	end.
