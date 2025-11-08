import React from "react";
import { Card, CardContent, Typography } from '@mui/material';
import QueryStatsIcon from '@mui/icons-material/QueryStats';
import SectionHeader from './SectionHeader';

interface QueryHistoryCardProps {
  recentQueryTimes: number[];
}

const QueryHistoryCard: React.FC<QueryHistoryCardProps> = ({ recentQueryTimes }) => (
  <Card>
    <CardContent>
      <SectionHeader icon={<QueryStatsIcon />} text="Recent Query Performance" />
      {recentQueryTimes.length > 0 ? (
        <div className="metrics-dashboard__history">
          {recentQueryTimes.slice(-20).map((time, index) => {
            const clamped = Math.min((time / 10) * 100, 100);
            const title = 'Query ' + (index + 1) + ': ' + time.toFixed(2) + 'ms';
            const className = 'metrics-dashboard__history-bar metrics-dashboard__history-bar--' +
              (time < 1 ? 'good' : time < 5 ? 'warn' : 'danger');
            return (
              <div
                key={index}
                className={className}
                style={{ height: clamped + '%' }}
                title={title}
              />
            );
          })}
        </div>
      ) : (
        <Typography variant="body2" color="text.secondary">
          No query data available. Perform some queries to see performance metrics.
        </Typography>
      )}
    </CardContent>
  </Card>
);

export default QueryHistoryCard;
