package com.guitaralchemist.musictheorydsl

import com.intellij.lang.*
import com.intellij.lang.annotation.*
import com.intellij.lexer.Lexer
import com.intellij.openapi.editor.DefaultLanguageHighlighterColors
import com.intellij.openapi.editor.colors.TextAttributesKey
import com.intellij.openapi.fileTypes.*
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.TextRange
import com.intellij.psi.*
import com.intellij.psi.tree.IElementType
import com.intellij.openapi.editor.highlighter.HighlighterIterator
import com.intellij.codeInsight.completion.*
import com.intellij.codeInsight.lookup.LookupElementBuilder
import com.intellij.patterns.PlatformPatterns
import com.intellij.util.ProcessingContext
import javax.swing.Icon

// ============================================================================
// CHORD PROGRESSION DSL
// ============================================================================

object ChordProgressionLanguage : Language("ChordProgression")

class ChordProgressionFileType : LanguageFileType(ChordProgressionLanguage) {
    override fun getName() = "Chord Progression"
    override fun getDescription() = "Chord Progression DSL file"
    override fun getDefaultExtension() = "chordprog"
    override fun getIcon(): Icon? = null
    
    companion object {
        val INSTANCE = ChordProgressionFileType()
    }
}

class ChordProgressionParserDefinition : ParserDefinition {
    override fun createLexer(project: Project?): Lexer = ChordProgressionLexer()
    override fun createParser(project: Project?): PsiParser = ChordProgressionParser()
    override fun getFileNodeType(): IFileElementType = FILE
    override fun getCommentTokens(): TokenSet = TokenSet.EMPTY
    override fun getStringLiteralElements(): TokenSet = TokenSet.EMPTY
    override fun createElement(node: ASTNode?): PsiElement = ChordProgressionPsiElement(node!!)
    override fun createFile(viewProvider: FileViewProvider): PsiFile = ChordProgressionFile(viewProvider)
    
    companion object {
        val FILE = IFileElementType(ChordProgressionLanguage)
    }
}

class ChordProgressionLexer : Lexer() {
    private var buffer: CharSequence = ""
    private var startOffset = 0
    private var endOffset = 0
    private var state = 0
    
    override fun start(buffer: CharSequence, startOffset: Int, endOffset: Int, initialState: Int) {
        this.buffer = buffer
        this.startOffset = startOffset
        this.endOffset = endOffset
        this.state = initialState
    }
    
    override fun getState() = state
    override fun getTokenType(): IElementType? = if (startOffset < endOffset) ChordProgressionTokenTypes.CHORD else null
    override fun getTokenStart() = startOffset
    override fun getTokenEnd() = endOffset
    override fun advance() { startOffset = endOffset }
    override fun getCurrentPosition(): LexerPosition = object : LexerPosition {
        override fun getOffset() = startOffset
        override fun getState() = state
    }
    override fun restore(position: LexerPosition) {
        startOffset = position.offset
        state = position.state
    }
    override fun getBufferSequence() = buffer
    override fun getBufferEnd() = endOffset
}

object ChordProgressionTokenTypes {
    val CHORD = IElementType("CHORD", ChordProgressionLanguage)
}

class ChordProgressionParser : PsiParser {
    override fun parse(root: IElementType, builder: PsiBuilder): ASTNode {
        val marker = builder.mark()
        while (!builder.eof()) {
            builder.advanceLexer()
        }
        marker.done(root)
        return builder.treeBuilt
    }
}

class ChordProgressionPsiElement(node: ASTNode) : ASTWrapperPsiElement(node)
class ChordProgressionFile(viewProvider: FileViewProvider) : PsiFileBase(viewProvider, ChordProgressionLanguage) {
    override fun getFileType() = ChordProgressionFileType.INSTANCE
}

class ChordProgressionSyntaxHighlighterFactory : SyntaxHighlighterFactory() {
    override fun getSyntaxHighlighter(project: Project?, virtualFile: com.intellij.openapi.vfs.VirtualFile?) =
        ChordProgressionSyntaxHighlighter()
}

class ChordProgressionSyntaxHighlighter : SyntaxHighlighterBase() {
    override fun getHighlightingLexer() = ChordProgressionLexer()
    override fun getTokenHighlights(tokenType: IElementType?): Array<TextAttributesKey> {
        return when (tokenType) {
            ChordProgressionTokenTypes.CHORD -> arrayOf(DefaultLanguageHighlighterColors.KEYWORD)
            else -> emptyArray()
        }
    }
}

class ChordProgressionCompletionContributor : CompletionContributor() {
    init {
        extend(CompletionType.BASIC, PlatformPatterns.psiElement(),
            object : CompletionProvider<CompletionParameters>() {
                override fun addCompletions(parameters: CompletionParameters, context: ProcessingContext, result: CompletionResultSet) {
                    // Roman numerals
                    listOf("I", "II", "III", "IV", "V", "VI", "VII", "i", "ii", "iii", "iv", "v", "vi", "vii").forEach {
                        result.addElement(LookupElementBuilder.create(it))
                    }
                    // Chord qualities
                    listOf("maj7", "min7", "dom7", "maj9", "min9", "dim", "aug").forEach {
                        result.addElement(LookupElementBuilder.create(it))
                    }
                }
            })
    }
}

class ChordProgressionAnnotator : Annotator {
    override fun annotate(element: PsiElement, holder: AnnotationHolder) {
        // Add validation logic here
    }
}

// ============================================================================
// FRETBOARD NAVIGATION DSL
// ============================================================================

object FretboardNavigationLanguage : Language("FretboardNavigation")

class FretboardNavigationFileType : LanguageFileType(FretboardNavigationLanguage) {
    override fun getName() = "Fretboard Navigation"
    override fun getDescription() = "Fretboard Navigation DSL file"
    override fun getDefaultExtension() = "fretboard"
    override fun getIcon(): Icon? = null
    
    companion object {
        val INSTANCE = FretboardNavigationFileType()
    }
}

class FretboardNavigationParserDefinition : ParserDefinition {
    override fun createLexer(project: Project?): Lexer = FretboardNavigationLexer()
    override fun createParser(project: Project?): PsiParser = FretboardNavigationParser()
    override fun getFileNodeType(): IFileElementType = FILE
    override fun getCommentTokens(): TokenSet = TokenSet.EMPTY
    override fun getStringLiteralElements(): TokenSet = TokenSet.EMPTY
    override fun createElement(node: ASTNode?): PsiElement = FretboardNavigationPsiElement(node!!)
    override fun createFile(viewProvider: FileViewProvider): PsiFile = FretboardNavigationFile(viewProvider)
    
    companion object {
        val FILE = IFileElementType(FretboardNavigationLanguage)
    }
}

class FretboardNavigationLexer : Lexer() {
    private var buffer: CharSequence = ""
    private var startOffset = 0
    private var endOffset = 0
    private var state = 0
    
    override fun start(buffer: CharSequence, startOffset: Int, endOffset: Int, initialState: Int) {
        this.buffer = buffer
        this.startOffset = startOffset
        this.endOffset = endOffset
        this.state = initialState
    }
    
    override fun getState() = state
    override fun getTokenType(): IElementType? = if (startOffset < endOffset) FretboardNavigationTokenTypes.POSITION else null
    override fun getTokenStart() = startOffset
    override fun getTokenEnd() = endOffset
    override fun advance() { startOffset = endOffset }
    override fun getCurrentPosition(): LexerPosition = object : LexerPosition {
        override fun getOffset() = startOffset
        override fun getState() = state
    }
    override fun restore(position: LexerPosition) {
        startOffset = position.offset
        state = position.state
    }
    override fun getBufferSequence() = buffer
    override fun getBufferEnd() = endOffset
}

object FretboardNavigationTokenTypes {
    val POSITION = IElementType("POSITION", FretboardNavigationLanguage)
}

class FretboardNavigationParser : PsiParser {
    override fun parse(root: IElementType, builder: PsiBuilder): ASTNode {
        val marker = builder.mark()
        while (!builder.eof()) {
            builder.advanceLexer()
        }
        marker.done(root)
        return builder.treeBuilt
    }
}

class FretboardNavigationPsiElement(node: ASTNode) : ASTWrapperPsiElement(node)
class FretboardNavigationFile(viewProvider: FileViewProvider) : PsiFileBase(viewProvider, FretboardNavigationLanguage) {
    override fun getFileType() = FretboardNavigationFileType.INSTANCE
}

class FretboardNavigationSyntaxHighlighterFactory : SyntaxHighlighterFactory() {
    override fun getSyntaxHighlighter(project: Project?, virtualFile: com.intellij.openapi.vfs.VirtualFile?) =
        FretboardNavigationSyntaxHighlighter()
}

class FretboardNavigationSyntaxHighlighter : SyntaxHighlighterBase() {
    override fun getHighlightingLexer() = FretboardNavigationLexer()
    override fun getTokenHighlights(tokenType: IElementType?): Array<TextAttributesKey> {
        return when (tokenType) {
            FretboardNavigationTokenTypes.POSITION -> arrayOf(DefaultLanguageHighlighterColors.KEYWORD)
            else -> emptyArray()
        }
    }
}

class FretboardNavigationCompletionContributor : CompletionContributor() {
    init {
        extend(CompletionType.BASIC, PlatformPatterns.psiElement(),
            object : CompletionProvider<CompletionParameters>() {
                override fun addCompletions(parameters: CompletionParameters, context: ProcessingContext, result: CompletionResultSet) {
                    listOf("position", "CAGED", "move", "slide", "string", "fret").forEach {
                        result.addElement(LookupElementBuilder.create(it))
                    }
                }
            })
    }
}

class FretboardNavigationAnnotator : Annotator {
    override fun annotate(element: PsiElement, holder: AnnotationHolder) {
        // Add validation logic here
    }
}

// ============================================================================
// SCALE TRANSFORMATION DSL (Simplified - similar pattern)
// ============================================================================

object ScaleTransformationLanguage : Language("ScaleTransformation")
class ScaleTransformationFileType : LanguageFileType(ScaleTransformationLanguage) {
    override fun getName() = "Scale Transformation"
    override fun getDescription() = "Scale Transformation DSL file"
    override fun getDefaultExtension() = "scaletrans"
    override fun getIcon(): Icon? = null
    companion object { val INSTANCE = ScaleTransformationFileType() }
}
class ScaleTransformationParserDefinition : ParserDefinition {
    override fun createLexer(project: Project?): Lexer = FretboardNavigationLexer()
    override fun createParser(project: Project?): PsiParser = FretboardNavigationParser()
    override fun getFileNodeType(): IFileElementType = FILE
    override fun getCommentTokens(): TokenSet = TokenSet.EMPTY
    override fun getStringLiteralElements(): TokenSet = TokenSet.EMPTY
    override fun createElement(node: ASTNode?): PsiElement = FretboardNavigationPsiElement(node!!)
    override fun createFile(viewProvider: FileViewProvider): PsiFile = ScaleTransformationFile(viewProvider)
    companion object { val FILE = IFileElementType(ScaleTransformationLanguage) }
}
class ScaleTransformationFile(viewProvider: FileViewProvider) : PsiFileBase(viewProvider, ScaleTransformationLanguage) {
    override fun getFileType() = ScaleTransformationFileType.INSTANCE
}
class ScaleTransformationSyntaxHighlighterFactory : SyntaxHighlighterFactory() {
    override fun getSyntaxHighlighter(project: Project?, virtualFile: com.intellij.openapi.vfs.VirtualFile?) =
        FretboardNavigationSyntaxHighlighter()
}
class ScaleTransformationCompletionContributor : CompletionContributor()
class ScaleTransformationAnnotator : Annotator {
    override fun annotate(element: PsiElement, holder: AnnotationHolder) {}
}

// ============================================================================
// GROTHENDIECK OPERATIONS DSL (Simplified - similar pattern)
// ============================================================================

object GrothendieckOperationsLanguage : Language("GrothendieckOperations")
class GrothendieckOperationsFileType : LanguageFileType(GrothendieckOperationsLanguage) {
    override fun getName() = "Grothendieck Operations"
    override fun getDescription() = "Grothendieck Operations DSL file"
    override fun getDefaultExtension() = "groth"
    override fun getIcon(): Icon? = null
    companion object { val INSTANCE = GrothendieckOperationsFileType() }
}
class GrothendieckOperationsParserDefinition : ParserDefinition {
    override fun createLexer(project: Project?): Lexer = FretboardNavigationLexer()
    override fun createParser(project: Project?): PsiParser = FretboardNavigationParser()
    override fun getFileNodeType(): IFileElementType = FILE
    override fun getCommentTokens(): TokenSet = TokenSet.EMPTY
    override fun getStringLiteralElements(): TokenSet = TokenSet.EMPTY
    override fun createElement(node: ASTNode?): PsiElement = FretboardNavigationPsiElement(node!!)
    override fun createFile(viewProvider: FileViewProvider): PsiFile = GrothendieckOperationsFile(viewProvider)
    companion object { val FILE = IFileElementType(GrothendieckOperationsLanguage) }
}
class GrothendieckOperationsFile(viewProvider: FileViewProvider) : PsiFileBase(viewProvider, GrothendieckOperationsLanguage) {
    override fun getFileType() = GrothendieckOperationsFileType.INSTANCE
}
class GrothendieckOperationsSyntaxHighlighterFactory : SyntaxHighlighterFactory() {
    override fun getSyntaxHighlighter(project: Project?, virtualFile: com.intellij.openapi.vfs.VirtualFile?) =
        FretboardNavigationSyntaxHighlighter()
}
class GrothendieckOperationsCompletionContributor : CompletionContributor()
class GrothendieckOperationsAnnotator : Annotator {
    override fun annotate(element: PsiElement, holder: AnnotationHolder) {}
}

