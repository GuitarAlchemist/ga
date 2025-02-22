import {FaExternalLinkSquareAlt} from 'react-icons/fa';

interface ScaleLinkProps {
    scale: number;
}

/**
 * A React functional component that renders a scale number and a link to an external
 * music theory resource for the given scale.
 *
 * @param {ScaleLinkProps} props - The properties for the ScaleLink component.
 * @param {number} props.scale - The scale number to display and link to.
 * @returns {React.ReactElement} A JSX element containing the scale number and an
 * external link icon that opens the corresponding scale page in a new tab.
 */
const ScaleLink: React.FC<ScaleLinkProps> = ({ scale }) => {
    const scaleLink = `https://ianring.com/musictheory/scales/${scale}`;
    return (
        <div>
            <text>{scale}</text>
            <a href={scaleLink} target="_blank" rel="noopener noreferrer">
                <FaExternalLinkSquareAlt style={{marginLeft: '5px', fontSize: '0.8em'}}/>
            </a>
        </div>
    );
};

export default ScaleLink;
