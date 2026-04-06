// src/components/PrimeRadiant/LibraryPanel.tsx
// Browse Jean-Pierre Petit's scientific comics (Savoir sans Frontieres) as curriculum references

import React, { useState, useCallback, useMemo } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
interface CurriculumReference {
  title: string;
  topic: string;
  departments: string[];
  level: string;
  curriculum_fit: string;
  archiveId?: string;  // Archive.org filename (without .pdf) within the collection
  collection?: string; // Archive.org collection identifier
  language: SupportedLanguage;
}

/** Archive.org collection per language — comics are files within these collections */
const ARCHIVE_COLLECTIONS: Partial<Record<SupportedLanguage, string>> = {
  en: 'TheseAnglaise',
  fr: 'SavoirSansFrontieres',
};

type SupportedLanguage = 'en' | 'fr' | 'es' | 'vi' | 'hi' | 'ar' | 'ru' | 'de' | 'sv' | 'tr' | 'cs' | 'ro' | 'it' | 'pt';

const SUPPORTED_LANGUAGES: SupportedLanguage[] = ['en', 'fr', 'es', 'vi', 'hi', 'ar', 'ru', 'de', 'sv', 'tr', 'cs', 'ro', 'it', 'pt'];

const LANGUAGE_LABELS: Record<SupportedLanguage, string> = {
  en: 'EN', fr: 'FR', es: 'ES', vi: 'VI', hi: 'HI', ar: 'AR',
  ru: 'RU', de: 'DE', sv: 'SV', tr: 'TR', cs: 'CS', ro: 'RO', it: 'IT', pt: 'PT',
};

const FLAG_ICON: Record<SupportedLanguage, string> = {
  en: '\uD83C\uDDEC\uD83C\uDDE7',
  fr: '\uD83C\uDDEB\uD83C\uDDF7',
  es: '\uD83C\uDDEA\uD83C\uDDF8',
  vi: '\uD83C\uDDFB\uD83C\uDDF3',
  hi: '\uD83C\uDDEE\uD83C\uDDF3',
  ar: '\uD83C\uDDF8\uD83C\uDDE6',
  ru: '\uD83C\uDDF7\uD83C\uDDFA',
  de: '\uD83C\uDDE9\uD83C\uDDEA',
  sv: '\uD83C\uDDF8\uD83C\uDDEA',
  tr: '\uD83C\uDDF9\uD83C\uDDF7',
  cs: '\uD83C\uDDE8\uD83C\uDDFF',
  ro: '\uD83C\uDDF7\uD83C\uDDF4',
  it: '\uD83C\uDDEE\uD83C\uDDF9',
  pt: '\uD83C\uDDF5\uD83C\uDDF9',
};

interface CurriculumData {
  source: string;
  license: string;
  archive: string;
  website: string;
  acknowledgement: string;
  references: CurriculumReference[];
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------
const DEPT_COLORS: Record<string, string> = {
  mathematics: '#FFD700',
  physics: '#4FC3F7',
  'computer-science': '#73d13d',
  music: '#FF6B6B',
  philosophy: '#CE93D8',
  'cognitive-science': '#FFB300',
  'product-management': '#FF8A65',
  futurology: '#80DEEA',
};

const LEVEL_ICON: Record<string, string> = {
  'middle-school to high-school': 'I',
  'high-school': 'II',
  'high-school to undergraduate': 'III',
  undergraduate: 'IV',
};

// ---------------------------------------------------------------------------
// Embedded curriculum data (static reference — no fetch needed)
// ---------------------------------------------------------------------------
const CURRICULUM_DATA: CurriculumData = {
  source: 'Jean-Pierre Petit \u2014 Savoir sans Fronti\u00e8res (Knowledge Without Borders)',
  license: 'Free for non-commercial educational use \u2014 duplication permitted, no profit, no political/sectarian/confessional connotations',
  archive: 'https://archive.org/details/TheseAnglaise',
  website: 'https://www.savoir-sans-frontieres.com/JPP/telechargeables/free_downloads.htm',
  acknowledgement: 'Scientific comics by Jean-Pierre Petit, astrophysicist, distributed freely by the Association Savoir sans Fronti\u00e8res',
  references: [
    // --- English editions ---
    { title: 'Topo the World', topic: 'Topology \u2014 surfaces, homeomorphisms, Boy\u2019s surface', departments: ['mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Foundations of topological thinking for TDA and Poincar\u00e9 embeddings', archiveId: 'Topo_the_world_eng', language: 'en' },
    { title: 'Here\u2019s Looking at Euclid', topic: 'Geometry \u2014 Euclidean constructions, geometric reasoning', departments: ['mathematics', 'physics'], level: 'middle-school to high-school', curriculum_fit: 'Geometric foundations for fretboard geometry and spatial reasoning', archiveId: 'HERE_S_LOOKING_AT_EUCLID', language: 'en' },
    { title: 'Bourbakof', topic: 'Abstract algebra \u2014 group theory, mathematical structures', departments: ['mathematics', 'music'], level: 'undergraduate', curriculum_fit: 'Group theory for pitch class groups, symmetry in music theory', archiveId: 'Bourbakof_en', language: 'en' },
    { title: 'Logotron', topic: 'Mathematical logic \u2014 formal systems, proof theory', departments: ['mathematics', 'philosophy', 'computer-science'], level: 'undergraduate', curriculum_fit: 'Foundations for tetravalent logic, formal verification, grammar theory', archiveId: 'logotron_eng', language: 'en' },
    { title: 'Computer Magic', topic: 'Computing \u2014 information theory, digital logic, algorithms', departments: ['computer-science'], level: 'high-school', curriculum_fit: 'Fundamentals for understanding constrained generation and digital signal processing', archiveId: 'COMPUTER_MAGIC', language: 'en' },
    { title: 'Run Robot Run', topic: 'Robotics \u2014 automation, control systems, agent behavior', departments: ['computer-science', 'cognitive-science'], level: 'high-school', curriculum_fit: 'Agent behavior foundations, governance as control theory analogy', archiveId: 'RUN_ROBOT_RUN', language: 'en' },
    { title: 'Everything is Relative', topic: 'Special and general relativity \u2014 spacetime, reference frames', departments: ['physics', 'mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Reference frame thinking applicable to multi-perspective governance', archiveId: 'EVERYTHING_IS_RELATIVE', language: 'en' },
    { title: 'The Silence Barrier', topic: 'Wave mechanics \u2014 sound, barriers, propagation', departments: ['physics', 'music'], level: 'high-school', curriculum_fit: 'Acoustics foundations for understanding string vibration and harmonics', archiveId: 'THE_SILENCE_BARRIER', language: 'en' },
    { title: 'The Black Hole', topic: 'Astrophysics \u2014 gravitational collapse, spacetime curvature', departments: ['physics', 'mathematics'], level: 'undergraduate', curriculum_fit: 'Extreme physics as thinking tool for boundary conditions and singularities', archiveId: 'THE_BLACK_HOLE', language: 'en' },
    { title: 'The Economicon', topic: 'Economics \u2014 market dynamics, resource allocation', departments: ['product-management'], level: 'high-school', curriculum_fit: 'Resource allocation thinking for budget management and prioritization', archiveId: 'The_Economicon', language: 'en' },
    { title: 'Big Bang', topic: 'Cosmology \u2014 origin of the universe, early physics', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Large-scale systems thinking, initial conditions and emergence', archiveId: 'BIG_BANG', language: 'en' },
    { title: 'A Cosmic Story', topic: 'Cosmology \u2014 history of the universe from Big Bang to life', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Narrative cosmology for big-picture systems thinking', archiveId: 'cosmicstory_eng-1', language: 'en' },
    { title: 'Flight of Fancy', topic: 'Aerodynamics \u2014 flight mechanics, lift and drag', departments: ['physics'], level: 'high-school', curriculum_fit: 'Fluid dynamics intuition applicable to signal flow analysis', archiveId: 'FLIGHT_OF_FANCY', language: 'en' },
    { title: 'A Thousand Billion Suns', topic: 'Stellar physics \u2014 star formation, nuclear fusion, supernovae', departments: ['physics'], level: 'high-school to undergraduate', curriculum_fit: 'Energy transformation and lifecycle thinking for system evolution', archiveId: 'thousand_billion_suns_eng', language: 'en' },
    { title: 'The Dark Side of the Universe', topic: 'Dark matter and dark energy \u2014 cosmological mysteries', departments: ['physics', 'futurology'], level: 'undergraduate', curriculum_fit: 'Unknown unknowns \u2014 epistemic humility in governance', archiveId: 'The_Dark_Side_of_the_Universe', language: 'en' },
    { title: 'For a Fistful of Amperes', topic: 'Electromagnetism \u2014 circuits, magnetism, EM waves', departments: ['physics'], level: 'high-school', curriculum_fit: 'Electromagnetic foundations for signal processing analogies', archiveId: 'For_a_fistful_of_amperes', language: 'en' },
    // --- French editions (originals) ---
    { title: 'Le Topologicon', topic: 'Topologie \u2014 surfaces, hom\u00e9omorphismes, surface de Boy', departments: ['mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Fondations de la pens\u00e9e topologique pour TDA et plongements de Poincar\u00e9', archiveId: 'le_topologicon', language: 'fr' },
    { title: 'Le Geometricon', topic: 'G\u00e9om\u00e9trie \u2014 constructions euclidiennes, raisonnement g\u00e9om\u00e9trique', departments: ['mathematics', 'physics'], level: 'middle-school to high-school', curriculum_fit: 'Fondations g\u00e9om\u00e9triques pour la g\u00e9om\u00e9trie du manche et le raisonnement spatial', archiveId: 'LE GEOMETRICON', language: 'fr' },
    { title: 'Les Aventures d\u2019Anselme Lanturlu: Bourbakof', topic: 'Alg\u00e8bre abstraite \u2014 th\u00e9orie des groupes, structures math\u00e9matiques', departments: ['mathematics', 'music'], level: 'undergraduate', curriculum_fit: 'Th\u00e9orie des groupes pour les classes de hauteurs, sym\u00e9trie en th\u00e9orie musicale', archiveId: 'Bourbakof_fr', language: 'fr' },
    { title: 'Le Logotron', topic: 'Logique math\u00e9matique \u2014 syst\u00e8mes formels, th\u00e9orie de la preuve', departments: ['mathematics', 'philosophy', 'computer-science'], level: 'undergraduate', curriculum_fit: 'Fondations pour la logique t\u00e9travalente, v\u00e9rification formelle, th\u00e9orie des grammaires', archiveId: 'logotron_fr', language: 'fr' },
    { title: 'L\u2019Informatique', topic: 'Informatique \u2014 th\u00e9orie de l\u2019information, logique num\u00e9rique, algorithmes', departments: ['computer-science'], level: 'high-school', curriculum_fit: 'Fondamentaux pour la g\u00e9n\u00e9ration contrainte et le traitement du signal num\u00e9rique', archiveId: 'MONEYBACK_DECOUVRE_L_INFORMATIQUE', language: 'fr' },
    { title: 'Robot mais pas trop', topic: 'Robotique \u2014 automatisation, syst\u00e8mes de contr\u00f4le, comportement d\u2019agents', departments: ['computer-science', 'cognitive-science'], level: 'high-school', curriculum_fit: 'Fondations du comportement d\u2019agents, gouvernance comme analogie de contr\u00f4le', archiveId: 'A QUOI REVENT LES ROBOTS', language: 'fr' },
    { title: 'Tout est relatif', topic: 'Relativit\u00e9 restreinte et g\u00e9n\u00e9rale \u2014 espace-temps, r\u00e9f\u00e9rentiels', departments: ['physics', 'mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Pens\u00e9e en r\u00e9f\u00e9rentiels applicable \u00e0 la gouvernance multi-perspectives', archiveId: 'toutestrelatifjpp', language: 'fr' },
    { title: 'Le Mur du Silence', topic: 'M\u00e9canique des ondes \u2014 son, barri\u00e8res, propagation', departments: ['physics', 'music'], level: 'high-school', curriculum_fit: 'Fondations acoustiques pour la vibration des cordes et les harmoniques', archiveId: 'LE MUR DU SILENCE', language: 'fr' },
    { title: 'Le Trou Noir', topic: 'Astrophysique \u2014 effondrement gravitationnel, courbure de l\u2019espace-temps', departments: ['physics', 'mathematics'], level: 'undergraduate', curriculum_fit: 'Physique extr\u00eame comme outil de r\u00e9flexion sur les conditions limites et singularit\u00e9s', archiveId: 'le_trou_noir', language: 'fr' },
    { title: 'L\u2019Economicon', topic: '\u00c9conomie \u2014 dynamiques de march\u00e9, allocation de ressources', departments: ['product-management'], level: 'high-school', curriculum_fit: 'Pens\u00e9e en allocation de ressources pour la gestion budg\u00e9taire et la priorisation', archiveId: 'ECONOMICON', language: 'fr' },
    { title: 'Big Bang', topic: 'Cosmologie \u2014 origine de l\u2019univers, physique primordiale', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Pens\u00e9e syst\u00e9mique \u00e0 grande \u00e9chelle, conditions initiales et \u00e9mergence', archiveId: 'big_bang_jpp', language: 'fr' },
    { title: 'Cosmic Story', topic: 'Cosmologie \u2014 histoire de l\u2019univers du Big Bang \u00e0 la vie', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Cosmologie narrative pour la pens\u00e9e syst\u00e9mique globale', archiveId: 'cosmic_story_fr', language: 'fr' },
    { title: 'Si on volait\u2009?', topic: 'A\u00e9rodynamique \u2014 m\u00e9canique du vol, portance et tra\u00een\u00e9e', departments: ['physics'], level: 'high-school', curriculum_fit: 'Intuition de dynamique des fluides applicable \u00e0 l\u2019analyse du flux de signaux', archiveId: 'mecavol', language: 'fr' },
    { title: 'Mille milliards de soleils', topic: 'Physique stellaire \u2014 formation d\u2019\u00e9toiles, fusion nucl\u00e9aire, supernovae', departments: ['physics'], level: 'high-school to undergraduate', curriculum_fit: 'Transformation d\u2019\u00e9nergie et pens\u00e9e en cycle de vie pour l\u2019\u00e9volution des syst\u00e8mes', archiveId: 'mille_milliards_de_soleils', language: 'fr' },
    { title: 'La Face cach\u00e9e de l\u2019Univers', topic: 'Mati\u00e8re noire et \u00e9nergie sombre \u2014 myst\u00e8res cosmologiques', departments: ['physics', 'futurology'], level: 'undergraduate', curriculum_fit: 'Inconnues inconnues \u2014 humilit\u00e9 \u00e9pist\u00e9mique en gouvernance', archiveId: 'la_face_cachee_de_lunivers', language: 'fr' },
    { title: 'Pour une poign\u00e9e d\u2019amp\u00e8res', topic: '\u00c9lectromagn\u00e9tisme \u2014 circuits, magn\u00e9tisme, ondes EM', departments: ['physics'], level: 'high-school', curriculum_fit: 'Fondations \u00e9lectromagn\u00e9tiques pour les analogies de traitement du signal', archiveId: 'ET POUR QUELQUES AMPERES DE PLUS', language: 'fr' },
    // --- Spanish (ESPANOL) ---
    { title: 'Topologicón', topic: 'Topología — superficies, homeomorfismos, superficie de Boy', departments: ['mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Fundamentos del pensamiento topológico para TDA y embeddings de Poincaré', archiveId: 'topologicon_es', language: 'es' },
    { title: 'Geometricón', topic: 'Geometría — construcciones euclidianas, razonamiento geométrico', departments: ['mathematics', 'physics'], level: 'middle-school to high-school', curriculum_fit: 'Fundamentos geométricos para la geometría del mástil y razonamiento espacial', archiveId: 'geometricon_es', language: 'es' },
    { title: 'Economicón', topic: 'Economía — dinámicas de mercado, asignación de recursos', departments: ['product-management'], level: 'high-school', curriculum_fit: 'Pensamiento en asignación de recursos para gestión presupuestaria', archiveId: 'economicon_es', language: 'es' },
    { title: 'Big Bang', topic: 'Cosmología — origen del universo, física primordial', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Pensamiento sistémico a gran escala, condiciones iniciales y emergencia', archiveId: 'big_bang_es', language: 'es' },
    { title: 'Todo es relativo', topic: 'Relatividad especial y general — espacio-tiempo, sistemas de referencia', departments: ['physics', 'mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Pensamiento en marcos de referencia aplicable a la gobernanza multi-perspectiva', archiveId: 'todo_es_relativo_es', language: 'es' },
    // --- Vietnamese (Vietnamien) ---
    { title: 'Topologicon', topic: 'Tô-pô học — mặt, phép đồng phôi, mặt Boy', departments: ['mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Nền tảng tư duy tô-pô cho TDA và nhúng Poincaré', archiveId: 'topologicon_vi', language: 'vi' },
    { title: 'Geometricon', topic: 'Hình học — các phép dựng Euclid, suy luận hình học', departments: ['mathematics', 'physics'], level: 'middle-school to high-school', curriculum_fit: 'Nền tảng hình học cho hình học cần đàn và suy luận không gian', archiveId: 'geometricon_vi', language: 'vi' },
    { title: 'Economicon', topic: 'Kinh tế học — động lực thị trường, phân bổ nguồn lực', departments: ['product-management'], level: 'high-school', curriculum_fit: 'Tư duy phân bổ nguồn lực cho quản lý ngân sách', archiveId: 'economicon_vi', language: 'vi' },
    { title: 'Big Bang', topic: 'Vũ trụ học — nguồn gốc vũ trụ, vật lý sơ khai', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Tư duy hệ thống quy mô lớn, điều kiện ban đầu và sự nổi lên', archiveId: 'big_bang_vi', language: 'vi' },
    { title: 'Tất cả là tương đối', topic: 'Thuyết tương đối — không-thời gian, hệ quy chiếu', departments: ['physics', 'mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Tư duy hệ quy chiếu áp dụng cho quản trị đa chiều', archiveId: 'tout_est_relatif_vi', language: 'vi' },
    // --- Hindi ---
    { title: 'Topologicon', topic: 'टोपोलॉजी — सतहें, होमियोमॉर्फिज़्म, बॉय सतह', departments: ['mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'TDA और Poincaré एम्बेडिंग्स के लिए टोपोलॉजिकल सोच की नींव', archiveId: 'topologicon_hi', language: 'hi' },
    { title: 'Geometricon', topic: 'ज्यामिति — यूक्लिडियन निर्माण, ज्यामितीय तर्क', departments: ['mathematics', 'physics'], level: 'middle-school to high-school', curriculum_fit: 'फ्रेटबोर्ड ज्यामिति और स्थानिक तर्क के लिए ज्यामितीय नींव', archiveId: 'geometricon_hi', language: 'hi' },
    { title: 'Economicon', topic: 'अर्थशास्त्र — बाज़ार गतिशीलता, संसाधन आवंटन', departments: ['product-management'], level: 'high-school', curriculum_fit: 'बजट प्रबंधन के लिए संसाधन आवंटन सोच', archiveId: 'economicon_hi', language: 'hi' },
    { title: 'Big Bang', topic: 'ब्रह्मांड विज्ञान — ब्रह्मांड की उत्पत्ति, प्रारंभिक भौतिकी', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'बड़े पैमाने पर प्रणालीगत सोच, प्रारंभिक स्थितियाँ और उद्भव', archiveId: 'big_bang_hi', language: 'hi' },
    { title: 'सब कुछ सापेक्ष है', topic: 'विशेष और सामान्य सापेक्षता — दिक्काल, संदर्भ फ्रेम', departments: ['physics', 'mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'बहु-परिप्रेक्ष्य शासन के लिए संदर्भ फ्रेम सोच', archiveId: 'tout_est_relatif_hi', language: 'hi' },
    // --- Arabic (Arabe) ---
    { title: 'توبولوجيكون', topic: 'طوبولوجيا — السطوح، التشاكل، سطح بوي', departments: ['mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'أسس التفكير الطوبولوجي لـ TDA وتضمينات بوانكاريه', archiveId: 'topologicon_ar', language: 'ar' },
    { title: 'جيومتريكون', topic: 'هندسة — إنشاءات إقليدية، استدلال هندسي', departments: ['mathematics', 'physics'], level: 'middle-school to high-school', curriculum_fit: 'الأسس الهندسية لهندسة لوحة الأصابع والاستدلال المكاني', archiveId: 'geometricon_ar', language: 'ar' },
    { title: 'إيكونوميكون', topic: 'اقتصاد — ديناميكيات السوق، تخصيص الموارد', departments: ['product-management'], level: 'high-school', curriculum_fit: 'تفكير تخصيص الموارد لإدارة الميزانية', archiveId: 'economicon_ar', language: 'ar' },
    { title: 'الانفجار العظيم', topic: 'علم الكون — أصل الكون، الفيزياء المبكرة', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'تفكير منظومي واسع النطاق، الظروف الأولية والنشوء', archiveId: 'big_bang_ar', language: 'ar' },
    { title: 'كل شيء نسبي', topic: 'النسبية الخاصة والعامة — الزمكان، أُطر المرجع', departments: ['physics', 'mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'تفكير إطار المرجع المطبق على الحوكمة متعددة المنظورات', archiveId: 'tout_est_relatif_ar', language: 'ar' },
    // --- Russian (Russe) ---
    { title: 'Топологикон', topic: 'Топология — поверхности, гомеоморфизмы, поверхность Боя', departments: ['mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Основы топологического мышления для TDA и вложений Пуанкаре', archiveId: 'topologicon_ru', language: 'ru' },
    { title: 'Геометрикон', topic: 'Геометрия — евклидовы построения, геометрические рассуждения', departments: ['mathematics', 'physics'], level: 'middle-school to high-school', curriculum_fit: 'Геометрические основы для геометрии грифа и пространственных рассуждений', archiveId: 'geometricon_ru', language: 'ru' },
    { title: 'Экономикон', topic: 'Экономика — рыночная динамика, распределение ресурсов', departments: ['product-management'], level: 'high-school', curriculum_fit: 'Мышление распределения ресурсов для бюджетного управления', archiveId: 'economicon_ru', language: 'ru' },
    { title: 'Большой взрыв', topic: 'Космология — происхождение Вселенной, ранняя физика', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Системное мышление масштаба, начальные условия и эмерджентность', archiveId: 'big_bang_ru', language: 'ru' },
    { title: 'Всё относительно', topic: 'Специальная и общая теория относительности — пространство-время, системы отсчёта', departments: ['physics', 'mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Мышление в системах отсчёта для мульти-перспективного управления', archiveId: 'tout_est_relatif_ru', language: 'ru' },
    // --- German (Deutch) ---
    { title: 'Topologikon', topic: 'Topologie — Flächen, Homöomorphismen, Boy-Fläche', departments: ['mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Grundlagen topologischen Denkens für TDA und Poincaré-Einbettungen', archiveId: 'topologicon_de', language: 'de' },
    { title: 'Geometrikon', topic: 'Geometrie — euklidische Konstruktionen, geometrisches Schlussfolgern', departments: ['mathematics', 'physics'], level: 'middle-school to high-school', curriculum_fit: 'Geometrische Grundlagen für Griffbrettgeometrie und räumliches Denken', archiveId: 'geometricon_de', language: 'de' },
    { title: 'Economikon', topic: 'Wirtschaft — Marktdynamik, Ressourcenallokation', departments: ['product-management'], level: 'high-school', curriculum_fit: 'Ressourcenallokationsdenken für Budgetmanagement', archiveId: 'economicon_de', language: 'de' },
    { title: 'Urknall', topic: 'Kosmologie — Ursprung des Universums, frühe Physik', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Systemdenken im großen Maßstab, Anfangsbedingungen und Emergenz', archiveId: 'big_bang_de', language: 'de' },
    { title: 'Alles ist relativ', topic: 'Spezielle und allgemeine Relativität — Raumzeit, Bezugssysteme', departments: ['physics', 'mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Bezugssystem-Denken für Multi-Perspektiv-Governance', archiveId: 'tout_est_relatif_de', language: 'de' },
    // --- Swedish (suedois) ---
    { title: 'Topologikon', topic: 'Topologi — ytor, homeomorfismer, Boys yta', departments: ['mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Grunder i topologiskt tänkande för TDA och Poincaré-inbäddningar', archiveId: 'topologicon_sv', language: 'sv' },
    { title: 'Geometrikon', topic: 'Geometri — euklidiska konstruktioner, geometriskt resonemang', departments: ['mathematics', 'physics'], level: 'middle-school to high-school', curriculum_fit: 'Geometriska grunder för greppbrädsgeometri och rumsligt resonemang', archiveId: 'geometricon_sv', language: 'sv' },
    { title: 'Economikon', topic: 'Ekonomi — marknadsdynamik, resursallokering', departments: ['product-management'], level: 'high-school', curriculum_fit: 'Resursallokeringstänkande för budgethantering', archiveId: 'economicon_sv', language: 'sv' },
    { title: 'Big Bang', topic: 'Kosmologi — universums ursprung, tidig fysik', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Systemtänkande i stor skala, initialvillkor och emergens', archiveId: 'big_bang_sv', language: 'sv' },
    { title: 'Allt är relativt', topic: 'Speciell och allmän relativitet — rumtid, referensramar', departments: ['physics', 'mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Referensramstänkande för multi-perspektiv styrning', archiveId: 'tout_est_relatif_sv', language: 'sv' },
    // --- Turkish (Turquie) ---
    { title: 'Topolojikon', topic: 'Topoloji — yüzeyler, homeomorfizmalar, Boy yüzeyi', departments: ['mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'TDA ve Poincaré gömmeleri için topolojik düşünce temelleri', archiveId: 'topologicon_tr', language: 'tr' },
    { title: 'Geometrikon', topic: 'Geometri — Öklid yapıları, geometrik akıl yürütme', departments: ['mathematics', 'physics'], level: 'middle-school to high-school', curriculum_fit: 'Klavye geometrisi ve uzamsal akıl yürütme için geometrik temeller', archiveId: 'geometricon_tr', language: 'tr' },
    { title: 'Ekonomikon', topic: 'Ekonomi — piyasa dinamikleri, kaynak tahsisi', departments: ['product-management'], level: 'high-school', curriculum_fit: 'Bütçe yönetimi için kaynak tahsisi düşüncesi', archiveId: 'economicon_tr', language: 'tr' },
    { title: 'Büyük Patlama', topic: 'Kozmoloji — evrenin kökeni, erken fizik', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Büyük ölçekli sistem düşüncesi, başlangıç koşulları ve ortaya çıkış', archiveId: 'big_bang_tr', language: 'tr' },
    { title: 'Her şey göreceli', topic: 'Özel ve genel görelilik — uzay-zaman, referans çerçeveleri', departments: ['physics', 'mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Çok perspektifli yönetişim için referans çerçevesi düşüncesi', archiveId: 'tout_est_relatif_tr', language: 'tr' },
    // --- Czech (tcheque) ---
    { title: 'Topologikon', topic: 'Topologie — povrchy, homeomorfismy, Boyův povrch', departments: ['mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Základy topologického myšlení pro TDA a Poincarého vnoření', archiveId: 'topologicon_cs', language: 'cs' },
    { title: 'Geometrikon', topic: 'Geometrie — euklidovské konstrukce, geometrické uvažování', departments: ['mathematics', 'physics'], level: 'middle-school to high-school', curriculum_fit: 'Geometrické základy pro geometrii hmatníku a prostorové uvažování', archiveId: 'geometricon_cs', language: 'cs' },
    { title: 'Ekonomikon', topic: 'Ekonomie — dynamika trhu, alokace zdrojů', departments: ['product-management'], level: 'high-school', curriculum_fit: 'Myšlení alokace zdrojů pro správu rozpočtu', archiveId: 'economicon_cs', language: 'cs' },
    { title: 'Velký třesk', topic: 'Kosmologie — původ vesmíru, raná fyzika', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Systémové myšlení velkého měřítka, počáteční podmínky a emergence', archiveId: 'big_bang_cs', language: 'cs' },
    { title: 'Vše je relativní', topic: 'Speciální a obecná relativita — časoprostor, vztažné soustavy', departments: ['physics', 'mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Myšlení vztažných soustav pro multi-perspektivní správu', archiveId: 'tout_est_relatif_cs', language: 'cs' },
    // --- Romanian (Roumain) ---
    { title: 'Topologicon', topic: 'Topologie — suprafețe, homeomorfisme, suprafața lui Boy', departments: ['mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Bazele gândirii topologice pentru TDA și încorporări Poincaré', archiveId: 'topologicon_ro', language: 'ro' },
    { title: 'Geometricon', topic: 'Geometrie — construcții euclidiene, raționament geometric', departments: ['mathematics', 'physics'], level: 'middle-school to high-school', curriculum_fit: 'Bazele geometrice pentru geometria tastaturii și raționamentul spațial', archiveId: 'geometricon_ro', language: 'ro' },
    { title: 'Economicon', topic: 'Economie — dinamica pieței, alocarea resurselor', departments: ['product-management'], level: 'high-school', curriculum_fit: 'Gândire de alocare a resurselor pentru managementul bugetului', archiveId: 'economicon_ro', language: 'ro' },
    { title: 'Big Bang', topic: 'Cosmologie — originea universului, fizica timpurie', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Gândire sistemică la scară largă, condiții inițiale și emergență', archiveId: 'big_bang_ro', language: 'ro' },
    { title: 'Totul e relativ', topic: 'Relativitate specială și generală — spațiu-timp, cadre de referință', departments: ['physics', 'mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Gândire în cadre de referință pentru guvernanță multi-perspectivă', archiveId: 'tout_est_relatif_ro', language: 'ro' },
    // --- Italian (Italien) ---
    { title: 'Topologicon', topic: 'Topologia — superfici, omeomorfismi, superficie di Boy', departments: ['mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Fondamenti del pensiero topologico per TDA e incorporamenti di Poincaré', archiveId: 'topologicon_it', language: 'it' },
    { title: 'Geometricon', topic: 'Geometria — costruzioni euclidee, ragionamento geometrico', departments: ['mathematics', 'physics'], level: 'middle-school to high-school', curriculum_fit: 'Fondamenti geometrici per la geometria della tastiera e ragionamento spaziale', archiveId: 'geometricon_it', language: 'it' },
    { title: 'Economicon', topic: 'Economia — dinamiche di mercato, allocazione delle risorse', departments: ['product-management'], level: 'high-school', curriculum_fit: 'Pensiero di allocazione delle risorse per la gestione del bilancio', archiveId: 'economicon_it', language: 'it' },
    { title: 'Big Bang', topic: 'Cosmologia — origine dell\'universo, fisica primordiale', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Pensiero sistemico su larga scala, condizioni iniziali ed emergenza', archiveId: 'big_bang_it', language: 'it' },
    { title: 'Tutto è relativo', topic: 'Relatività speciale e generale — spazio-tempo, sistemi di riferimento', departments: ['physics', 'mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Pensiero in sistemi di riferimento per la governance multi-prospettica', archiveId: 'tout_est_relatif_it', language: 'it' },
    // --- Portuguese (Portuguais) ---
    { title: 'Topologicon', topic: 'Topologia — superfícies, homeomorfismos, superfície de Boy', departments: ['mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Fundamentos do pensamento topológico para TDA e incorporações de Poincaré', archiveId: 'topologicon_pt', language: 'pt' },
    { title: 'Geometricon', topic: 'Geometria — construções euclidianas, raciocínio geométrico', departments: ['mathematics', 'physics'], level: 'middle-school to high-school', curriculum_fit: 'Fundamentos geométricos para a geometria do braço e raciocínio espacial', archiveId: 'geometricon_pt', language: 'pt' },
    { title: 'Economicon', topic: 'Economia — dinâmicas de mercado, alocação de recursos', departments: ['product-management'], level: 'high-school', curriculum_fit: 'Pensamento de alocação de recursos para gestão orçamentária', archiveId: 'economicon_pt', language: 'pt' },
    { title: 'Big Bang', topic: 'Cosmologia — origem do universo, física primordial', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Pensamento sistêmico em grande escala, condições iniciais e emergência', archiveId: 'big_bang_pt', language: 'pt' },
    { title: 'Tudo é relativo', topic: 'Relatividade especial e geral — espaço-tempo, referenciais', departments: ['physics', 'mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Pensamento em referenciais para governança multi-perspectiva', archiveId: 'tout_est_relatif_pt', language: 'pt' },
  ],
};

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
/** Detect browser locale to pick default language */
function getDefaultLanguage(): SupportedLanguage {
  if (typeof navigator !== 'undefined') {
    const lang = (navigator.language || '').toLowerCase();
    const prefix = lang.split('-')[0] as SupportedLanguage;
    if (SUPPORTED_LANGUAGES.includes(prefix)) return prefix;
  }
  return 'en';
}

export const LibraryPanel: React.FC = () => {
  const [collapsed, setCollapsed] = useState(false);
  const [expandedItems, setExpandedItems] = useState<Set<string>>(new Set());
  const [deptFilter, setDeptFilter] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [readingComic, setReadingComic] = useState<CurriculumReference | null>(null);
  const [language, setLanguage] = useState<SupportedLanguage>(getDefaultLanguage);

  const data = CURRICULUM_DATA;
  const refs = data.references;

  const toggleItem = useCallback((title: string) => {
    setExpandedItems(prev => {
      const next = new Set(prev);
      if (next.has(title)) next.delete(title);
      else next.add(title);
      return next;
    });
  }, []);

  // Apply language, department, and text search filters
  const filtered = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    return refs.filter(r => {
      if (r.language !== language) return false;
      if (deptFilter && !r.departments.includes(deptFilter)) return false;
      if (q && !r.title.toLowerCase().includes(q) && !r.topic.toLowerCase().includes(q)) return false;
      return true;
    });
  }, [refs, deptFilter, searchQuery, language]);

  // Count only comics in the selected language
  const langCount = useMemo(() => refs.filter(r => r.language === language).length, [refs, language]);

  // Collect all departments for filter chips
  const allDepts = useMemo(
    () => [...new Set(refs.flatMap(r => r.departments))].sort(),
    [refs],
  );

  return (
    <div className="prime-radiant__library">
      <div
        className="prime-radiant__library-header"
        onClick={() => setCollapsed(!collapsed)}
      >
        <span className="prime-radiant__library-title">
          Library
          <span className="prime-radiant__library-count">{langCount}</span>
        </span>
        <span className="prime-radiant__library-toggle">{collapsed ? '▶' : '▼'}</span>
      </div>

      {!collapsed && (
        <div className="prime-radiant__library-body">
          {/* Source attribution */}
          <div className="prime-radiant__library-source">
            <span className="prime-radiant__library-source-label">Jean-Pierre Petit</span>
            <span className="prime-radiant__library-source-sub">Savoir sans Fronti&egrave;res</span>
            <div className="prime-radiant__library-source-links">
              <a href={data.website} target="_blank" rel="noopener noreferrer" className="prime-radiant__library-link">Downloads</a>
              <a href={data.archive} target="_blank" rel="noopener noreferrer" className="prime-radiant__library-link">Archive.org</a>
            </div>
            <span className="prime-radiant__library-license">{data.license}</span>
          </div>

          {/* Language toggle */}
          <div className="prime-radiant__library-lang-toggle">
            {SUPPORTED_LANGUAGES.map(lang => {
              const count = refs.filter(r => r.language === lang).length;
              return (
                <button
                  key={lang}
                  className={`prime-radiant__library-lang-btn${language === lang ? ' prime-radiant__library-lang-btn--active' : ''}`}
                  onClick={(e) => { e.stopPropagation(); setLanguage(lang); }}
                  title={`${LANGUAGE_LABELS[lang]} (${count} comics)`}
                >
                  {FLAG_ICON[lang]} {LANGUAGE_LABELS[lang]} ({count})
                </button>
              );
            })}
          </div>

          {/* Text search */}
          <div className="prime-radiant__library-search">
            <input
              type="text"
              className="prime-radiant__library-search-input"
              placeholder="Search by title or topic..."
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
            />
            {searchQuery && (
              <button
                className="prime-radiant__library-search-clear"
                onClick={() => setSearchQuery('')}
                aria-label="Clear search"
              >&#x2715;</button>
            )}
          </div>

          {/* Department filter chips */}
          <div className="prime-radiant__library-filters">
            <button
              className={`prime-radiant__library-filter ${deptFilter === null ? 'prime-radiant__library-filter--active' : ''}`}
              onClick={() => setDeptFilter(null)}
            >All</button>
            {allDepts.map(dept => (
              <button
                key={dept}
                className={`prime-radiant__library-filter ${deptFilter === dept ? 'prime-radiant__library-filter--active' : ''}`}
                style={deptFilter === dept ? { borderColor: DEPT_COLORS[dept] ?? '#888', color: DEPT_COLORS[dept] ?? '#888' } : undefined}
                onClick={() => setDeptFilter(deptFilter === dept ? null : dept)}
              >{dept.replace('-', ' ')}</button>
            ))}
          </div>

          {/* Reference list */}
          <div className="prime-radiant__library-list">
            {filtered.map(ref => {
              const expanded = expandedItems.has(ref.title);
              const levelIcon = Object.entries(LEVEL_ICON).find(([k]) => ref.level.includes(k))?.[1] ?? 'II';
              return (
                <div key={`${ref.language}-${ref.title}`} className="prime-radiant__library-item">
                  <div
                    className="prime-radiant__library-item-header"
                    onClick={() => toggleItem(ref.title)}
                  >
                    <span className="prime-radiant__library-item-level" title={ref.level}>{levelIcon}</span>
                    <span className="prime-radiant__library-item-title">{FLAG_ICON[ref.language]} {ref.title}</span>
                    <span className="prime-radiant__library-item-chevron">{expanded ? '▼' : '▶'}</span>
                  </div>
                  <div className="prime-radiant__library-item-topic">{ref.topic}</div>
                  <div className="prime-radiant__library-item-depts">
                    {ref.departments.map(d => (
                      <span
                        key={d}
                        className="prime-radiant__library-dept-tag"
                        style={{ color: DEPT_COLORS[d] ?? '#888', borderColor: `${DEPT_COLORS[d] ?? '#888'}44` }}
                      >{d.replace('-', ' ')}</span>
                    ))}
                  </div>
                  {expanded && (
                    <div className="prime-radiant__library-item-detail">
                      <div className="prime-radiant__library-item-fit">
                        <span className="prime-radiant__library-item-fit-label">Curriculum fit</span>
                        {ref.curriculum_fit}
                      </div>
                      <div className="prime-radiant__library-item-meta">
                        Level: {ref.level}
                      </div>
                      {ref.archiveId && (
                        <div className="prime-radiant__library-item-actions">
                          <button
                            className="prime-radiant__library-read-btn"
                            onClick={(e) => { e.stopPropagation(); setReadingComic(ref); }}
                          >
                            Read Comic
                          </button>
                          <a
                            href={`https://archive.org/download/TheseAnglaise/${ref.archiveId}.pdf`}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="prime-radiant__library-download-btn"
                            onClick={(e) => e.stopPropagation()}
                          >
                            PDF
                          </a>
                        </div>
                      )}
                    </div>
                  )}
                </div>
              );
            })}
          </div>

          {filtered.length === 0 && (
            <div className="prime-radiant__library-empty">
              {searchQuery ? 'No comics match your search' : 'No references in this department'}
            </div>
          )}

          {/* License attribution footer */}
          <div className="prime-radiant__library-footer">
            {data.acknowledgement}
          </div>
        </div>
      )}

      {/* PDF Reader overlay */}
      {readingComic && readingComic.archiveId && (
        <div className="prime-radiant__library-reader-overlay">
          <div className="prime-radiant__library-reader">
            <div className="prime-radiant__library-reader-header">
              <div className="prime-radiant__library-reader-title">
                <span className="prime-radiant__library-reader-book">{readingComic.title}</span>
                <span className="prime-radiant__library-reader-author">Jean-Pierre Petit &mdash; Savoir sans Fronti&egrave;res</span>
              </div>
              <div className="prime-radiant__library-reader-actions">
                <a
                  href={`https://archive.org/download/TheseAnglaise/${readingComic.archiveId}.pdf`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="prime-radiant__library-reader-dl"
                >Download PDF</a>
                <button
                  className="prime-radiant__library-reader-close"
                  onClick={() => setReadingComic(null)}
                >&#x2715;</button>
              </div>
            </div>
            <div className="prime-radiant__library-reader-legal">
              Public Domain (Mark 1.0) &mdash; Free for educational use. Scientific comics by Jean-Pierre Petit, astrophysicist.
              Distributed by Association Savoir sans Fronti&egrave;res. Source: Archive.org
            </div>
            <iframe
              className="prime-radiant__library-reader-frame"
              src={(() => {
                const col = readingComic.collection ?? ARCHIVE_COLLECTIONS[readingComic.language];
                return col
                  ? `https://archive.org/download/${col}/${encodeURIComponent(readingComic.archiveId ?? '')}.pdf`
                  : `https://archive.org/download/${readingComic.archiveId}`;
              })()}
              title={readingComic.title}
            />
          </div>
        </div>
      )}
    </div>
  );
};
